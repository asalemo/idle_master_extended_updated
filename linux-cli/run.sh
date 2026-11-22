#!/bin/sh
set -eu
cd "$(dirname "$0")"

case "${LANG:-}" in
    fr*) lang=fr ;;
    *) lang=en ;;
esac
say() { if [ "$lang" = fr ]; then printf '%s\n' "$2"; else printf '%s\n' "$1"; fi; }
fail() { say "$1" "$2" >&2; exit "$3"; }

if [ "$#" -eq 1 ] && { [ "$1" = "--help" ] || [ "$1" = "-h" ]; }; then
    echo "./run.sh <AppID>"
    exit 0
fi
if [ "$#" -ne 1 ] || ! printf '%s\n' "$1" | awk '/^[0-9]+$/ && $0+0 > 0 && $0+0 <= 4294967295 { ok=1 } END { exit !ok }'; then
    fail "Usage: ./run.sh <positive numeric AppID>" "Utilisation : ./run.sh <AppID numérique positif>" 2
fi
[ "$(uname -s)/$(uname -m)" = Linux/x86_64 ] ||
    fail "Use x64 Linux." "Utilise Linux x64." 2
if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -Eq '^(8|9|[1-9][0-9]+)\.' ||
    ! dotnet --list-runtimes | grep -q '^Microsoft.NETCore.App 8\.'; then
    fail "Install the .NET 8 SDK, then run this command again." "Installe le SDK .NET 8 puis relance cette commande." 3
fi

digest=eb2dd015b84177cf4f4326fe578aab375fd8931bbbd719c7492420d9777007fe
url=https://raw.githubusercontent.com/rlabrecque/SteamworksSDK/df2baabf574a738ef1ea90a7e89339107fc0a279/redistributable_bin/linux64/libsteam_api.so
verify() { printf '%s  %s\n' "$digest" "$1" | sha256sum --check --status; }
if [ ! -f libsteam_api.so ]; then
    say "Downloading the Steamworks Linux library…" "Téléchargement de la bibliothèque Steamworks Linux…"
    tmp=$(mktemp ./libsteam_api.so.XXXXXX)
    trap 'rm -f "$tmp"' EXIT
    curl -fsSL "$url" -o "$tmp" ||
        fail "Could not download the Steam library. Check curl and your connection." "Téléchargement impossible. Vérifie curl et ta connexion." 3
    verify "$tmp" || fail "Steam library checksum mismatch." "La somme de contrôle de la bibliothèque Steam est incorrecte." 3
    mv "$tmp" libsteam_api.so
else
    verify libsteam_api.so || fail "Unexpected Steam library. Move it aside and try again." "Bibliothèque Steam inattendue. Déplace-la puis réessaie." 3
fi

DOTNET_NOLOGO=1 dotnet build IdleMaster.Linux.csproj -c Release --nologo -v quiet >/dev/null ||
    fail "Build failed. Run dotnet build -c Release for details." "Échec de compilation. Exécute dotnet build -c Release pour les détails." 3
exec dotnet bin/Release/net8.0/IdleMaster.Linux.dll "$1"
