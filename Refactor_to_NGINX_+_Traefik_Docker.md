# Refactor: NGINX (host) + Traefik (Docker)

**Fecha:** 2026-07-21
**Dominio:** `rmrlocations.com` — API en `api.rmrlocations.com`
**VPS:** `92.118.59.247` (Ubuntu)

---

## 1. Arquitectura

```
                    ┌──────────────────────── VPS 92.118.59.247 ────────────────────────┐
                    │                                                                    │
Internet ──:443──►  │  NGINX (host)                                                      │
                    │   · termina TLS con certificados de Certbot/Let's Encrypt          │
                    │   · añade X-Real-IP, X-Forwarded-For, X-Forwarded-Proto            │
                    │   · cabeceras de seguridad + HSTS                                  │
                    │         │ proxy_pass                                               │
                    │         ▼                                                          │
                    │  127.0.0.1:8081  ◄── cerrado a internet (binding loopback)         │
                    │         │                                                          │
                    │  Traefik (Docker)                                                  │
                    │   · enruta por Host() según labels                                 │
                    │   · confía y propaga las X-Forwarded-* de NGINX                    │
                    │         │  red traefik-public                                      │
                    │         ▼                                                          │
                    │  api:8080 (ASP.NET Core, HTTP en claro)                            │
                    │   · UseForwardedHeaders() reconstruye IP y esquema reales          │
                    │         │  red interna (sin salida)                                │
                    │         ▼                                                          │
                    │  postgres:5432 (PostGIS)                                           │
                    └────────────────────────────────────────────────────────────────────┘
```

### Por qué dos proxies

NGINX cubre lo que vive **en el host** (y las apps no dockerizadas que añadas después);
Traefik cubre lo que vive **en contenedores**, descubriéndolos solo por sus labels.
Añadir una nueva app en Docker no requiere tocar NGINX: basta con sus labels.

**El coste, dicho con honestidad:** son dos configuraciones que mantener, un salto de red
extra y una cadena de confianza de cabeceras más larga. Si todo lo que vas a desplegar
va a ser Docker, Traefik solo (con su propio ACME) sería más simple. La doble capa
gana cuando mezclas contenedores con servicios instalados directamente en el host.

### Cambio de modelo de certificados

| | Antes | Ahora |
|---|---|---|
| Quién emite | Traefik (ACME TLS-ALPN-01) | **Certbot en el host** (ACME HTTP-01 webroot) |
| Dónde se guardan | volumen Docker `acme.json` | `/etc/letsencrypt/` del host |
| Quién termina TLS | Traefik | **NGINX** |
| Puertos 80/443 | Traefik | **NGINX** |
| Traefik escucha en | `:80`, `:443` públicos | **`127.0.0.1:8081`** |

---

## 2. Archivos

### Nuevos

| Archivo | Destino en el VPS |
|---|---|
| `traefik/traefik.yaml` | montado en el contenedor como `/etc/traefik/traefik.yaml` |
| `deploy/nginx/00-common.conf` | `/etc/nginx/conf.d/00-common.conf` |
| `deploy/nginx/api.rmrlocations.com.bootstrap.conf` | `/etc/nginx/sites-available/` (temporal, Fase 1) |
| `deploy/nginx/api.rmrlocations.com.conf` | `/etc/nginx/sites-available/` (definitivo, Fase 2) |

### Modificados

**`docker-compose.yml`** — reescrito:
- Traefik pasa de `traefik:latest` a `traefik:v3` (versión fijada: `latest` puede meter un cambio incompatible en un `pull` rutinario).
- Puertos: de `80:80` + `443:443` a **`127.0.0.1:8081:8081`**.
- Eliminados el resolver ACME, el entrypoint `websecure` y el volumen `traefik-certificates`.
- Configuración estática movida de flags CLI a `traefik/traefik.yaml`.
- Router de la API: entrypoint `web`, **sin labels de TLS**.
- Cabeceras de seguridad retiradas de Traefik → ahora en NGINX (evita duplicarlas).
- `POSTGRES_PASSWORD` y `JWT_TOKEN` pasan a ser **obligatorios** (`${VAR:?}`).
- PostgreSQL solo en la red `internal`: inalcanzable desde Traefik, NGINX o internet.
- Healthchecks en Traefik y PostgreSQL.

**`Program.cs`** — añadido `UseForwardedHeaders()` como primer middleware, con
`KnownNetworks`/`KnownProxies` vaciados. Es el tercer eslabón de la cadena de IP real:
sin él, NGINX y Traefik propagan la IP del cliente pero la API sigue viendo la de Traefik.

**`.env.template`** — `DOMAIN=rmrlocations.com`; secretos vacíos con instrucciones de
generación; eliminado `TRAEFIK_AUTH_USERS` (el dashboard ya no se publica).

**Frontend** — `.env` → `VITE_API_URL=https://api.rmrlocations.com`; comentario de
`worker.js` actualizado.

---

## 3. 🔴 Antes de empezar: lee esto

**Habrá caída de servicio.** Traefik ocupa hoy los puertos 80 y 443. NGINX los necesita.
No pueden convivir, así que hay que parar Traefik antes de instalar NGINX. Cuenta con
**10-20 minutos** de API caída.

**Los secretos van a cambiar.** El `.env` actual usa la contraseña de PostgreSQL y la
clave JWT por defecto del repositorio. Ambas están publicadas en GitHub — con esa clave
JWT cualquiera puede fabricar tokens de administrador válidos. Se regeneran en el Paso 7.

**Trampa de la contraseña de PostgreSQL:** el volumen `postgres-data` ya existe y guarda
la contraseña con la que se inicializó. Si solo la cambias en el `.env`, la API no podrá
conectar. Hay que cambiarla **también dentro de la base de datos** (Paso 7.3).

---

## 4. Despliegue paso a paso

Todo esto se ejecuta **por SSH en el VPS**. Sustituye `usuario` por tu usuario real.

```bash
ssh usuario@92.118.59.247
cd /ruta/del/proyecto        # el directorio con docker-compose.yml
```

### Paso 1 — Respaldo

```bash
cp docker-compose.yml docker-compose.yml.bak
cp .env .env.bak
docker compose ps                       # anota qué está corriendo
docker exec rmrlocations-postgres pg_dump -U postgres rmrLocationsApi > ~/backup-$(date +%F).sql
```

### Paso 2 — Liberar los puertos 80 y 443

```bash
docker compose down
sudo ss -tlnp | grep -E ':80|:443'      # no debe devolver nada
```

**A partir de aquí la API está caída.**

### Paso 3 — Instalar NGINX y Certbot

```bash
sudo apt update
sudo apt install -y nginx certbot
```

No instales `python3-certbot-nginx`: usamos el método **webroot**, que no toca la
configuración de NGINX.

### Paso 4 — Subir los archivos

Desde **tu PC**, en otra terminal:

```bash
cd "C:/Users/INGENIERO RAMON/Dropbox/Visual_Studio_Projects/ASP.NET Projects/rmrLocationsApi"
scp deploy/nginx/00-common.conf                        usuario@92.118.59.247:/tmp/
scp deploy/nginx/api.rmrlocations.com.bootstrap.conf   usuario@92.118.59.247:/tmp/
scp deploy/nginx/api.rmrlocations.com.conf             usuario@92.118.59.247:/tmp/
scp docker-compose.yml                                 usuario@92.118.59.247:/tmp/
scp -r traefik                                         usuario@92.118.59.247:/tmp/
```

### Paso 5 — Fase 1: NGINX mínimo para el desafío ACME

```bash
# Directorio donde Certbot dejará el archivo de prueba
sudo mkdir -p /var/www/certbot
sudo chown -R www-data:www-data /var/www/certbot

# Configuración de arranque (solo puerto 80)
sudo cp /tmp/api.rmrlocations.com.bootstrap.conf /etc/nginx/sites-available/api.rmrlocations.com.conf
sudo ln -sf /etc/nginx/sites-available/api.rmrlocations.com.conf /etc/nginx/sites-enabled/

# Elimina el sitio por defecto de Ubuntu (chocaría con nuestro default_server)
sudo rm -f /etc/nginx/sites-enabled/default

sudo nginx -t          # DEBE decir "syntax is ok" y "test is successful"
sudo systemctl restart nginx
sudo systemctl enable nginx
```

Comprueba **desde tu PC** que responde:

```bash
curl http://api.rmrlocations.com/
# Esperado: "Bootstrap NGINX activo. Pendiente de emitir certificado."
```

⚠️ **No sigas si esto no responde.** Certbot fallará y Let's Encrypt limita a
**5 validaciones fallidas por hora y por dominio**.

### Paso 6 — Emitir el certificado con Certbot

```bash
sudo certbot certonly \
  --webroot \
  --webroot-path /var/www/certbot \
  --domain api.rmrlocations.com \
  --email montoyacode@gmail.com \
  --agree-tos \
  --no-eff-email \
  --deploy-hook "systemctl reload nginx"
```

**Qué hace cada opción:**

| Opción | Función |
|---|---|
| `certonly` | Solo emite; no toca la configuración de NGINX |
| `--webroot --webroot-path` | Escribe el desafío en `/var/www/certbot`, que NGINX ya publica |
| `--deploy-hook` | Recarga NGINX **cada vez que renueve**. Sin esto seguiría sirviendo el certificado viejo hasta el siguiente reinicio |
| `--no-eff-email` | Evita la suscripción al boletín de la EFF |

Si prefieres ensayar antes sin gastar cuota, añade `--dry-run` (no emite nada real).

Verifica:

```bash
sudo certbot certificates
sudo ls -l /etc/letsencrypt/live/api.rmrlocations.com/
# Deben existir: fullchain.pem, privkey.pem, chain.pem, cert.pem
```

### Paso 7 — Fase 2: configuración definitiva de NGINX

```bash
sudo cp /tmp/00-common.conf /etc/nginx/conf.d/00-common.conf
sudo cp /tmp/api.rmrlocations.com.conf /etc/nginx/sites-available/api.rmrlocations.com.conf

sudo nginx -t
sudo systemctl reload nginx
```

### Paso 8 — Secretos nuevos

```bash
# 8.1 Genera valores únicos
openssl rand -base64 32     # → POSTGRES_PASSWORD
openssl rand -base64 64     # → JWT_TOKEN

# 8.2 Edita el .env
nano .env
```

El `.env` debe quedar así (con **tus** valores generados):

```
DOMAIN=rmrlocations.com
LETSENCRYPT_EMAIL=montoyacode@gmail.com
POSTGRES_PASSWORD=<el valor de 32 bytes>
JWT_TOKEN=<el valor de 64 bytes>
JWT_ISSUER=rmrLocationsApi
JWT_AUDIENCE=rmrLocationsApi
```

```bash
# 8.3 ⚠️ Sincroniza la contraseña DENTRO de PostgreSQL
# El volumen conserva la contraseña original; cambiar solo el .env rompe la conexión.
sudo cp /tmp/docker-compose.yml ./docker-compose.yml
sudo cp -r /tmp/traefik ./traefik

docker compose up -d postgres
sleep 10
docker compose exec postgres psql -U postgres -c "ALTER USER postgres WITH PASSWORD 'PEGA_AQUI_TU_NUEVA_PASSWORD';"
```

### Paso 9 — Levantar la pila completa

```bash
docker compose up -d --remove-orphans
docker compose ps            # los tres servicios en estado "running"/"healthy"
docker compose logs traefik | tail -20
```

### Paso 10 — Cortafuegos

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow OpenSSH        # ⚠️ ANTES de habilitar, o pierdes el acceso
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status verbose
```

---

## 5. 🔴 Seguridad: por qué UFW **no** protege el puerto de Traefik

Esta es la lección más importante de todo el documento.

**Docker inserta sus propias reglas de iptables por delante de las de UFW.** Si publicas
un puerto como `-p 8081:8081`, queda accesible desde internet **aunque UFW lo bloquee**.
`ufw status` mostrará "DENY" y el puerto seguirá abierto. Es una causa habitual de bases
de datos expuestas por accidente.

**La protección real es el binding**, que es lo que hace nuestro `docker-compose.yml`:

```yaml
ports:
  - "127.0.0.1:8081:8081"     # ← el prefijo IP es lo que cierra el puerto
```

Con ese prefijo, Docker solo abre el puerto en la interfaz loopback. No existe desde
fuera del host, sin importar lo que diga el cortafuegos.

### Defensa en profundidad (opcional pero recomendable)

Por si algún día alguien quita el prefijo `127.0.0.1` sin darse cuenta:

```bash
# Regla en la cadena DOCKER-USER, la única que Docker respeta
sudo iptables -I DOCKER-USER -p tcp --dport 8081 ! -i lo -j DROP

# Persistir tras reinicios
sudo apt install -y iptables-persistent
sudo netfilter-persistent save
```

### Verificación desde fuera

Desde tu PC, **no** desde el VPS (en local siempre responderá):

```bash
curl -m 5 http://92.118.59.247:8081/     # debe dar timeout o "connection refused"
nmap -Pn -p 80,443,8081 92.118.59.247    # 8081 debe salir filtered/closed
```

---

## 6. Renovación automática

Certbot instala un `systemd timer` que se encarga solo. No hay que programar nada.

```bash
systemctl list-timers | grep certbot     # confirma que está activo
sudo certbot renew --dry-run             # ensayo completo sin emitir nada
```

El `--deploy-hook` del Paso 6 quedó guardado en
`/etc/letsencrypt/renewal/api.rmrlocations.com.conf`, así que cada renovación recarga
NGINX automáticamente.

El certificado dura 90 días y Certbot renueva a partir del día 60. El bloque
`location ^~ /.well-known/acme-challenge/` del puerto 80 **debe seguir existiendo**:
las renovaciones lo vuelven a usar.

---

## 7. Verificación final

```bash
# 1. Certificado válido y emitido por Let's Encrypt (sin -k)
openssl s_client -connect api.rmrlocations.com:443 -servername api.rmrlocations.com \
  </dev/null 2>/dev/null | openssl x509 -noout -subject -issuer -dates

# 2. La API responde con validación de certificado activada
curl -s -o /dev/null -w "%{http_code}\n" https://api.rmrlocations.com/api/category

# 3. Redirección HTTP → HTTPS
curl -sI http://api.rmrlocations.com/api/category | head -3

# 4. Cabeceras de seguridad
curl -sI https://api.rmrlocations.com/api/category | grep -iE "strict-transport|x-frame|x-content"

# 5. Puerto de Traefik cerrado (desde tu PC)
curl -m 5 http://92.118.59.247:8081/

# 6. Login real en el navegador
#    https://rmrlocations.montoyacode.workers.dev/login
```

### Frontend

`VITE_API_URL` se inlinea en tiempo de build y `.env` está en `.gitignore`. Si Cloudflare
construye el proyecto, define `VITE_API_URL=https://api.rmrlocations.com` en las
variables de build del panel (Workers & Pages → Settings → Variables) y redespliega.
Si subes el `dist/` ya compilado, el build local está hecho y verificado.

---

## 8. ✅ EJECUTADO Y VERIFICADO — 2026-07-21

El runbook se ejecutó completo sobre el VPS. **La arquitectura está en producción y
funcionando.**

### Verificación externa (desde fuera del VPS)

| Comprobación | Resultado |
|---|---|
| Certificado | ✅ `CN=api.rmrlocations.com`, emisor `Let's Encrypt CN=YE1` |
| Vigencia | ✅ 2026-07-21 → **2026-10-19** |
| API sin `-k` (validación TLS activa) | ✅ **HTTP 200**, `ssl_verify_result: 0` |
| Redirección HTTP → HTTPS | ✅ `301 Moved Permanently` |
| HSTS | ✅ `max-age=31536000` |
| `X-Content-Type-Options` / `X-Frame-Options` / `Referrer-Policy` | ✅ Presentes |
| **Puerto 8081 desde internet** | ✅ **Inalcanzable** |
| Puerto 8081 en el host | ✅ `LISTEN 127.0.0.1:8081` (solo loopback) |
| IP real del cliente en logs | ✅ IPs públicas reales, no loopback |
| `certbot renew --dry-run` | ✅ *"all simulated renewals succeeded"* |
| `certbot.timer` | ✅ Activo, próxima ejecución en ~13 h |
| Contenedores | ✅ Los 3 `healthy` |

### Desviaciones respecto al plan

Tres cosas no salieron a la primera. Se corrigieron y **los archivos del repositorio ya
incorporan las correcciones**:

1. **`http2 on;` no existe en NGINX 1.24.** Esa directiva llegó en la 1.25.1; Ubuntu
   24.04 trae la 1.24.0. Corregido a la sintaxis antigua `listen 443 ssl http2;`.
   NGINX rechazó la configuración y siguió sirviendo la anterior, sin caída añadida.

2. **`ssl_stapling` provocaba un warning.** Let's Encrypt dejó de incluir la URL del
   respondedor OCSP en sus certificados, así que la directiva no tenía efecto.
   Eliminada.

3. **Traefik arrancó `unhealthy`.** El healthcheck invoca `traefik healthcheck --ping`,
   pero faltaba la sección `ping:` en `traefik.yaml` que habilita ese endpoint. Traefik
   enrutaba correctamente; era el chequeo lo que estaba mal. Añadida la sección.
   Ojo: la configuración **estática** de Traefik solo se recarga reiniciando el
   contenedor — `docker compose up -d` no basta si el `docker-compose.yml` no cambió.

### Simplificaciones descubiertas en el servidor

- **`POSTGRES_PASSWORD` ya no era la del repositorio.** Se dejó intacta, evitando por
  completo el `ALTER USER` y su riesgo asociado.
- **`JWT_TOKEN` sí seguía siendo la clave publicada en GitHub** — un agujero de
  seguridad activo en producción. Rotada con `openssl rand -hex 48` durante el corte.
  Las sesiones previas quedaron invalidadas, que era el efecto deseado.
- **UFW ya estaba activo** con `OpenSSH`, `80/tcp` y `443/tcp`. No hizo falta tocarlo.

### Respaldos disponibles para revertir

- `/home/deploy/backup-pre-nginx.sql` — volcado de PostgreSQL (31 KB)
- `/home/apps/rmrLocationsApi/docker-compose.yml.bak`
- `/home/apps/rmrLocationsApi/.env.bak`

### Verificaciones previas en local

`docker compose config` válido · router `Host(api.rmrlocations.com)` en entrypoint `web`
sin TLS · binding `host_ip: 127.0.0.1` · aborta si falta `DOMAIN` · `dotnet build` 0
errores 0 warnings · `npm run build` correcto con la URL nueva inlineada.

### ⬜ Único paso pendiente

**Redesplegar el frontend** para que apunte a `https://api.rmrlocations.com`. Es tarea
tuya en el panel de Cloudflare — ver sección 7, apartado *Frontend*.

---

## 9. Si algo falla

| Síntoma | Causa probable | Solución |
|---|---|---|
| `nginx -t` → "duplicate default server" | Quedó el sitio por defecto de Ubuntu | `sudo rm /etc/nginx/sites-enabled/default` |
| `nginx: cannot load certificate` | Se instaló la config definitiva sin tener certificado | Vuelve a la bootstrap, emite, y luego cambia |
| Certbot: "Timeout during connect" | El puerto 80 no llega desde internet | Revisa UFW y que NGINX esté escuchando |
| Certbot: "too many failed authorizations" | 5 fallos en una hora | Espera 1 hora. Usa `--dry-run` para ensayar |
| API responde 404 vía NGINX | Traefik no reconoce el Host | Revisa `DOMAIN` en `.env` y `docker compose logs traefik` |
| API responde 502 | Traefik caído o puerto equivocado | `docker compose ps`; verifica `127.0.0.1:8081` |
| La API no conecta a PostgreSQL | Contraseña cambiada solo en `.env` | Ejecuta el `ALTER USER` del Paso 8.3 |
| Los logs muestran IP de loopback | Falta algún eslabón de la cadena | Verifica `proxy_set_header` en NGINX, `trustedIPs` en Traefik y `UseForwardedHeaders()` |

### Volver atrás

```bash
docker compose down
sudo systemctl stop nginx && sudo systemctl disable nginx
cp docker-compose.yml.bak docker-compose.yml
cp .env.bak .env
docker compose up -d
```

---

## 10. Dashboard de Traefik

No se publica: sería una superficie de administración expuesta a internet. Para
consultarlo, túnel SSH sin abrir ningún puerto:

1. En `traefik/traefik.yaml`, cambia `insecure: false` → `insecure: true`
2. En `docker-compose.yml`, añade `- "127.0.0.1:8080:8080"` a los `ports` de Traefik
3. `docker compose up -d traefik`
4. Desde tu PC: `ssh -L 8080:127.0.0.1:8080 usuario@92.118.59.247`
5. Abre `http://localhost:8080/dashboard/`
6. **Revierte los cambios al terminar**

---

## 11. Añadir una aplicación nueva más adelante

Este es el beneficio de la arquitectura. Para una app dockerizada en `app2.rmrlocations.com`:

1. **DNS:** registro A `app2` → `92.118.59.247` en Cloudflare, **nube gris**
2. **Certificado:**
   ```bash
   sudo certbot certonly --webroot --webroot-path /var/www/certbot \
     --domain app2.rmrlocations.com --email montoyacode@gmail.com \
     --agree-tos --no-eff-email --deploy-hook "systemctl reload nginx"
   ```
3. **NGINX:** copia `api.rmrlocations.com.conf`, cambia `server_name` y las rutas de
   certificado. El `proxy_pass` sigue siendo `http://127.0.0.1:8081` — Traefik enruta por
   el `Host`.
4. **Contenedor:** añádelo a la red `traefik-public` con sus labels:
   ```yaml
   labels:
     - "traefik.enable=true"
     - "traefik.http.routers.app2.rule=Host(`app2.rmrlocations.com`)"
     - "traefik.http.routers.app2.entrypoints=web"
     - "traefik.http.services.app2.loadbalancer.server.port=<puerto interno>"
   ```

Cuando tengas varios subdominios, plantéate migrar a un **certificado wildcard**
(`*.rmrlocations.com`) por DNS-01 con la API de Cloudflare: un solo certificado y nunca
más ejecutas Certbot al añadir una app.

---

## 12. Pendientes conocidos

- **`.github/workflows/deploy.yml` no copia los archivos de configuración al VPS.** Solo
  hace `docker compose pull api` + `up -d`. `docker-compose.yml`, `traefik/` y los
  archivos de NGINX se mantienen a mano. Merece la pena añadir un paso de `scp` al
  workflow para que la infraestructura se despliegue igual que el código.
- **`app.UseHttpsRedirection()` en `Program.cs`** queda inerte: dentro del contenedor
  Kestrel solo escucha HTTP y no puede determinar un puerto HTTPS. Con
  `UseForwardedHeaders()` ya en su sitio, `Request.Scheme` es `https` y el middleware no
  redirige. No molesta, pero es ruido: detrás de un proxy, la redirección corresponde a
  NGINX, que ya la hace.
- **`worker.js`, bloque `catch`:** devuelve `200 "Serving..."` ante cualquier error de
  assets, convirtiendo fallos de despliegue en éxitos silenciosos.
- **CORS:** `Program.cs` acepta `*.workers.dev` y `*.pages.dev`. Si algún día sirves el
  frontend desde `rmrlocations.com`, habrá que añadir ese origen.
