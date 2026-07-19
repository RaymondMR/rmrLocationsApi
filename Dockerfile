# =============================================
# ETAPA 1: BUILD — Compilar la aplicación .NET
# =============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copiar archivo .csproj y restaurar dependencias (capa cacheable)
COPY *.csproj .
RUN dotnet restore

# Copiar el resto del código y publicar en Release
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# =============================================
# ETAPA 2: RUNTIME — Solo lo necesario para ejecutar
# =============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Instalar curl para HEALTHCHECK
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Crear usuario no-root por seguridad
RUN useradd -m -u 1001 -s /bin/bash appuser

# Copiar archivos publicados desde la etapa build
COPY --from=build /app .

# Asignar propiedad al usuario no-root
RUN chown -R appuser:appuser /app

# Cambiar a usuario no-root
USER appuser

# Configurar puerto y entorno
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Habilitar Forwarded Headers para que la app sepa que Traefik maneja HTTPS
# (evita loops de redirección con UseHttpsRedirection)
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 8080

# Health check — verifica que la API responda
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "rmrLocationsApi.dll"]
