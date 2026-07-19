#!/bin/bash
BASE="https://localhost:5113"
TMP=$(mktemp -d)
PASS=0 FAIL=0
TS=$(date +%s)

check() {
  local desc="$1" expected="$2" actual="$3"
  if [ "$expected" = "ANY" ]; then
    if [ -n "$actual" ] && [ "$actual" != "null" ] && [ ${#actual} -gt 10 ]; then
      echo "  [PASS] $desc"
      PASS=$((PASS+1))
    else
      echo "  [FAIL] $desc (no valid value: '$actual')"
      FAIL=$((FAIL+1))
    fi
  elif [ "$actual" = "$expected" ]; then
    echo "  [PASS] $desc ($expected)"
    PASS=$((PASS+1))
  else
    echo "  [FAIL] $desc (expected $expected, got $actual)"
    FAIL=$((FAIL+1))
  fi
}

extract() {
  local key="$1" json="$2"
  echo "$json" | grep -o "\"$key\":\"[^\"]*\"" | head -1 | cut -d'"' -f4
}

echo "============================================"
echo " API FINAL TEST SUITE - TS=$TS"
echo "============================================"

# Use unique names to avoid DB conflicts
UN="tester-${TS}"
EM="${UN}@test.com"

# ---- 1. Register ----
echo ""; echo "--- AUTH: Register ---"
REG=$(curl -sk -X POST "$BASE/api/auth/register" -H "Content-Type: application/json" -d "{\"userName\":\"$UN\",\"email\":\"$EM\",\"password\":\"Test123!\"}")
echo "Resp: ${REG:0:150}"
USER_ID=$(extract "id" "$REG")
check "Register" "ANY" "$USER_ID"

# ---- 2. Login ----
echo ""; echo "--- AUTH: Login ---"
LOGIN=$(curl -sk -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" -d "{\"userName\":\"$UN\",\"email\":\"$EM\",\"password\":\"Test123!\"}")
ACC_TOKEN=$(extract "accessToken" "$LOGIN")
REF_TOKEN=$(extract "refreshToken" "$LOGIN")
check "Login accessToken" "ANY" "$ACC_TOKEN"
check "Login refreshToken" "ANY" "$REF_TOKEN"

# ---- 3. Auth endpoints ----
echo ""; echo "--- AUTH: Endpoints ---"
A1=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/auth" -H "Authorization: Bearer $ACC_TOKEN")
check "Auth-only GET" "200" "$A1"

ADMIN_LOGIN=$(curl -sk -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" -d '{"userName":"admin","email":"admin@rmrlocations.com","password":"Admin123!"}')
ADMIN_TOK=$(extract "accessToken" "$ADMIN_LOGIN")
check "Admin login" "ANY" "$ADMIN_TOK"
A2=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/auth/admin-only" -H "Authorization: Bearer $ADMIN_TOK")
check "Admin-only" "200" "$A2"

# ---- 4. Refresh token ----
echo ""; echo "--- AUTH: Refresh ---"
REFR=$(curl -sk -X POST "$BASE/api/auth/refresh-token" -H "Content-Type: application/json" -d "{\"userId\":\"$USER_ID\",\"refreshToken\":\"$REF_TOKEN\"}")
NEW_AT=$(extract "accessToken" "$REFR")
check "Refresh token" "ANY" "$NEW_AT"

# ---- 5. Change password ----
echo ""; echo "--- AUTH: Change Password ---"
C1=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/change-password" -H "Authorization: Bearer $ACC_TOKEN" -H "Content-Type: application/json" -d '{"currentPassword":"Test123!","newPassword":"Test456!"}')
check "Change password" "200" "$C1"
C2=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/change-password" -H "Authorization: Bearer $ACC_TOKEN" -H "Content-Type: application/json" -d '{"currentPassword":"Test456!","newPassword":"Test123!"}')
check "Change back" "200" "$C2"

# ---- 6. LOCATION CRUD ----
echo ""; echo "--- LOCATION: CRUD ---"
SLUG="tc-${TS}"
cat > "$TMP/loc.json" << ENDJSON
{"name":"Test Cafe ${TS}","slug":"$SLUG","description":"Cafeteria","coordinates":{"X":-99.1332,"Y":19.4326},"address":{"name":"Centro","street":"Av. Reforma","exteriorNumber":"222","neighborhood":"Juarez","city":"CDMX","state":"CDMX","postalCode":"06600","countryCode":"MX"},"status":"Published","isPublic":true,"websiteUrl":"https://test.com","phoneNumber":"+525512345678","averageRating":0,"reviewCount":0,"ownerId":"$USER_ID"}
ENDJSON
LOC=$(curl -sk -X POST "$BASE/api/location" -H "Content-Type: application/json" -d @"$TMP/loc.json")
echo "Resp: ${LOC:0:250}"
LOC_ID=$(extract "id" "$LOC")
check "Create location" "ANY" "$LOC_ID"

GET_L=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/location/$LOC_ID")
check "Get location by ID" "200" "$GET_L"

GET_ALL=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/location")
check "Get all locations" "200" "$GET_ALL"

SLUG2="tc-mod-${TS}"
cat > "$TMP/loc2.json" << ENDJSON
{"id":"$LOC_ID","name":"Test Cafe MOD ${TS}","slug":"$SLUG2","description":"Actualizado","coordinates":{"X":-99.1332,"Y":19.4326},"address":{"name":"MOD","street":"Av. Reforma","exteriorNumber":"333","neighborhood":"Juarez","city":"CDMX","state":"CDMX","postalCode":"06600","countryCode":"MX"},"status":"Published","isPublic":true,"websiteUrl":"https://test.com","phoneNumber":"+525512345678","averageRating":0,"reviewCount":0,"ownerId":"$USER_ID"}
ENDJSON
P_LOC=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/location/$LOC_ID" -H "Content-Type: application/json" -d @"$TMP/loc2.json")
check "Update location" "204" "$P_LOC"

# ---- 7. Geo ----
echo ""; echo "--- LOCATION: Geo ---"
NR=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/location/nearby?latitude=19.4326&longitude=-99.1332&radiusMeters=5000&take=10")
check "Nearby search" "200" "$NR"
SR=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/location/search?latitude=19.4326&longitude=-99.1332&radiusMeters=5000")
check "Combined search" "200" "$SR"

# ---- 8. CATEGORY CRUD ----
echo ""; echo "--- CATEGORY: CRUD ---"
cat > "$TMP/cat.json" << ENDJSON
{"name":"Cat-${TS}","slug":"cat-${TS}","description":"Cafes","iconName":"coffee","colorHex":"#8B4513","sortOrder":1}
ENDJSON
CAT=$(curl -sk -X POST "$BASE/api/category" -H "Content-Type: application/json" -d @"$TMP/cat.json")
echo "Resp: ${CAT:0:200}"
CAT_ID=$(extract "id" "$CAT")
check "Create category" "ANY" "$CAT_ID"
GC=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/category/$CAT_ID")
check "Get category by ID" "200" "$GC"
cat > "$TMP/cat2.json" << ENDJSON
{"id":"$CAT_ID","name":"Cat-MOD-${TS}","slug":"cat-mod-${TS}","description":"Mod","iconName":"coffee","colorHex":"#A0522D","sortOrder":2}
ENDJSON
PC=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/category/$CAT_ID" -H "Content-Type: application/json" -d @"$TMP/cat2.json")
check "Update category" "204" "$PC"
DC=$(curl -sk -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/category/$CAT_ID")
check "Delete category" "204" "$DC"

# ---- 9. TAG CRUD ----
echo ""; echo "--- TAG: CRUD ---"
cat > "$TMP/tag.json" << ENDJSON
{"name":"t-${TS}","slug":"t-${TS}","description":"WiFi"}
ENDJSON
TAG=$(curl -sk -X POST "$BASE/api/tag" -H "Content-Type: application/json" -d @"$TMP/tag.json")
echo "Resp: ${TAG:0:200}"
TAG_ID=$(extract "id" "$TAG")
check "Create tag" "ANY" "$TAG_ID"
GT=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/tag/$TAG_ID")
check "Get tag by ID" "200" "$GT"
cat > "$TMP/tag2.json" << ENDJSON
{"id":"$TAG_ID","name":"t-mod-${TS}","slug":"t-mod-${TS}","description":"Premium"}
ENDJSON
PT=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/tag/$TAG_ID" -H "Content-Type: application/json" -d @"$TMP/tag2.json")
check "Update tag" "204" "$PT"
DT=$(curl -sk -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/tag/$TAG_ID")
check "Delete tag" "204" "$DT"

# ---- 10. REVIEW CRUD ----
echo ""; echo "--- REVIEW: CRUD ---"
cat > "$TMP/rev.json" << ENDJSON
{"locationId":"$LOC_ID","userId":"$USER_ID","rating":4,"title":"Buen cafe","body":"Rico espresso"}
ENDJSON
REV=$(curl -sk -X POST "$BASE/api/review" -H "Content-Type: application/json" -d @"$TMP/rev.json")
echo "Resp: ${REV:0:200}"
REV_ID=$(extract "id" "$REV")
check "Create review" "ANY" "$REV_ID"
GR=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/review/$REV_ID")
check "Get review by ID" "200" "$GR"
RL=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/review/location/$LOC_ID")
check "Reviews by location" "200" "$RL"
cat > "$TMP/rev2.json" << ENDJSON
{"id":"$REV_ID","locationId":"$LOC_ID","userId":"$USER_ID","rating":5,"title":"Excelente!","body":"Aun mejor"}
ENDJSON
PR=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/review/$REV_ID" -H "Content-Type: application/json" -d @"$TMP/rev2.json")
check "Update review" "204" "$PR"
DR=$(curl -sk -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/review/$REV_ID")
check "Delete review" "204" "$DR"

# ---- 11. MEDIA CRUD ----
echo ""; echo "--- MEDIA: CRUD ---"
cat > "$TMP/med.json" << ENDJSON
{"locationId":"$LOC_ID","url":"https://img.com/x-${TS}.jpg","thumbnailUrl":"https://img.com/t-${TS}.jpg","caption":"Fachada","type":"Image","isCover":true,"sortOrder":1,"uploadedByUserId":"$USER_ID"}
ENDJSON
MED=$(curl -sk -X POST "$BASE/api/media" -H "Content-Type: application/json" -d @"$TMP/med.json")
echo "Resp: ${MED:0:200}"
MED_ID=$(extract "id" "$MED")
check "Create media" "ANY" "$MED_ID"
GM=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/media/$MED_ID")
check "Get media by ID" "200" "$GM"
ML=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/media/location/$LOC_ID")
check "Media by location" "200" "$ML"
cat > "$TMP/med2.json" << ENDJSON
{"id":"$MED_ID","locationId":"$LOC_ID","url":"https://img.com/y-${TS}.jpg","thumbnailUrl":"https://img.com/yt-${TS}.jpg","caption":"Upd","type":"Image","isCover":true,"sortOrder":1,"uploadedByUserId":"$USER_ID"}
ENDJSON
PM=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/media/$MED_ID" -H "Content-Type: application/json" -d @"$TMP/med2.json")
check "Update media" "204" "$PM"
DM=$(curl -sk -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/media/$MED_ID")
check "Delete media" "204" "$DM"

# ---- 12. COLLECTION CRUD ----
echo ""; echo "--- COLLECTION: CRUD ---"
cat > "$TMP/coll.json" << ENDJSON
{"name":"MisFavs-${TS}","description":"Coleccion","visibility":"Public","ownerId":"$USER_ID"}
ENDJSON
COLL=$(curl -sk -X POST "$BASE/api/collection" -H "Content-Type: application/json" -d @"$TMP/coll.json")
echo "Resp: ${COLL:0:200}"
COLL_ID=$(extract "id" "$COLL")
check "Create collection" "ANY" "$COLL_ID"
GCOL=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/collection/$COLL_ID")
check "Get collection by ID" "200" "$GCOL"
CU=$(curl -sk -o /dev/null -w "%{http_code}" "$BASE/api/collection/user/$USER_ID")
check "Collections by user" "200" "$CU"
cat > "$TMP/coll2.json" << ENDJSON
{"id":"$COLL_ID","name":"MisFavs-MOD-${TS}","description":"Actualizada","visibility":"Unlisted","ownerId":"$USER_ID"}
ENDJSON
PCOL=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/collection/$COLL_ID" -H "Content-Type: application/json" -d @"$TMP/coll2.json")
check "Update collection" "204" "$PCOL"
AI=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/collection/$COLL_ID/items" -H "Content-Type: application/json" -d "{\"locationId\":\"$LOC_ID\",\"notes\":\"Favorito\"}")
check "Add item to collection" "204" "$AI"
RI=$(curl -sk -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/collection/$COLL_ID/items/$LOC_ID")
check "Remove item from collection" "204" "$RI"
DCOL=$(curl -sk -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/collection/$COLL_ID")
check "Delete collection" "204" "$DCOL"

# ---- 13. Soft delete + Restore ----
echo ""; echo "--- LOCATION: Soft Delete + Restore ---"
SD=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/location/$LOC_ID/soft-delete")
check "Soft delete location" "204" "$SD"
RS=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/location/$LOC_ID/restore")
check "Restore location" "204" "$RS"

# ---- 14. Logout ----
echo ""; echo "--- AUTH: Logout ---"
LO=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/logout" -H "Authorization: Bearer $ACC_TOKEN")
check "Logout" "200" "$LO"

# ---- 15. CLEANUP ----
echo ""; echo "--- CLEANUP ---"
# Delete location (cascades to media, reviews, opening_hours)
DL=$(curl -sk -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/location/$LOC_ID")
check "Delete location" "204" "$DL"

# Remove temp dir
rm -rf "$TMP"

echo ""
echo "============================================"
echo " RESULTS: $PASS passed, $FAIL failed"
echo "============================================"

if [ "$FAIL" -gt 0 ]; then
  echo "SOME TESTS FAILED!"
  exit 1
else
  echo "ALL TESTS PASSED!"
  exit 0
fi
