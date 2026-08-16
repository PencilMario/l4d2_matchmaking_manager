#!/usr/bin/env bash
set -e

if [ "${ENABLE_STEAM:-}" = "true" ]; then
    steam_root="${USER_HOME:-/home/default}/.steam/steam"
    library_directory="${steam_root}/steamapps"
    library_file="${library_directory}/libraryfolders.vdf"
    steam_config_directory="${steam_root}/config"
    steam_config_file="${steam_config_directory}/config.vdf"

    if [ -n "${STEAM_SHARED_LIBRARY_PATH:-}" ]; then
        install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "${library_directory}"
        temporary_file="$(mktemp "${library_file}.tmp.XXXXXX")"
        printf '%s\n' \
            '"libraryfolders"' \
            '{' \
            '    "0"' \
            '    {' \
            "        \"path\" \"${steam_root}\"" \
            '        "label" "Home"' \
            '        "apps" {}' \
            '    }' \
            '    "1"' \
            '    {' \
            "        \"path\" \"${STEAM_SHARED_LIBRARY_PATH}\"" \
            '        "label" "Shared L4D2"' \
            '        "apps" {}' \
            '    }' \
            '}' > "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0600 "${temporary_file}"
        mv "${temporary_file}" "${library_file}"
    fi

    install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "${steam_config_directory}"
    if [ ! -f "${steam_config_file}" ]; then
        temporary_file="$(mktemp "${steam_config_file}.tmp.XXXXXX")"
        printf '%s\n' \
            '"InstallConfigStore"' \
            '{' \
            '    "Software"' \
            '    {' \
            '        "Valve"' \
            '        {' \
            '            "Steam"' \
            '            {' \
            '                "ShaderCacheManager"' \
            '                {' \
            '                    "DisableShaderCache" "1"' \
            '                }' \
            '            }' \
            '        }' \
            '    }' \
            '}' > "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0600 "${temporary_file}"
        mv "${temporary_file}" "${steam_config_file}"
    elif grep -q '"DisableShaderCache"' "${steam_config_file}"; then
        sed -i 's|"DisableShaderCache"[[:space:]]*"[^"]*"|"DisableShaderCache" "1"|' "${steam_config_file}"
    elif grep -q '"ShaderCacheManager"' "${steam_config_file}"; then
        sed -i '/^[[:space:]]*"ShaderCacheManager"[[:space:]]*$/ { n; a\
                    "DisableShaderCache" "1"
        }' "${steam_config_file}"
    else
        sed -i '/^[[:space:]]*"Steam"[[:space:]]*$/ { n; a\
                "ShaderCacheManager"\
                {\
                    "DisableShaderCache" "1"\
                }
        }' "${steam_config_file}"
    fi
    chown "${PUID:-1000}:${PGID:-1000}" "${steam_config_file}"
    chmod 0600 "${steam_config_file}"

    sed -i 's|^autostart=.*$|autostart=true|' /etc/supervisor.d/steam.ini
fi
