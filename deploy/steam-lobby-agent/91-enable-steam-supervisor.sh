#!/usr/bin/env bash
set -e

if [ "${ENABLE_STEAM:-}" = "true" ]; then
    steam_root="${USER_HOME:-/home/default}/.steam/steam"
    library_directory="${steam_root}/steamapps"
    library_file="${library_directory}/libraryfolders.vdf"
    steam_config_directory="${steam_root}/config"
    steam_config_file="${steam_config_directory}/config.vdf"

    set_l4d2_update_policy() {
        manifest="${STEAM_SHARED_LIBRARY_PATH:-}/steamapps/appmanifest_550.acf"
        [ -n "${STEAM_SHARED_LIBRARY_PATH:-}" ] && [ -f "${manifest}" ] || return 0
        sed -i 's|"AutoUpdateBehavior"[[:space:]]*"[^"]*"|"AutoUpdateBehavior" "1"|' "${manifest}"
    }

    set_download_region() {
        download_region="${STEAM_DOWNLOAD_REGION:-}"
        [ -n "${download_region}" ] || return 0
        if printf '%s' "${download_region}" | grep -q '["\r\n]'; then
            echo 'STEAM_DOWNLOAD_REGION must not contain a quote or newline.' >&2
            return 1
        fi

        escaped_download_region="$(printf '%s' "${download_region}" | sed 's/[\\&|]/\\&/g')"
        if grep -q '"DownloadRegion"' "${steam_config_file}"; then
            sed -i "s|\"DownloadRegion\"[[:space:]]*\"[^\"]*\"|\"DownloadRegion\" \"${escaped_download_region}\"|" "${steam_config_file}"
            return 0
        fi

        temporary_file="$(mktemp "${steam_config_file}.tmp.XXXXXX")"
        awk -v region="${download_region}" '
            /^[[:space:]]*"Steam"[[:space:]]*$/ && !inserted {
                print
                if (getline > 0) print
                print "                \"DownloadRegion\" \"" region "\""
                inserted = 1
                next
            }
            { print }
            END { if (!inserted) exit 1 }
        ' "${steam_config_file}" > "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0600 "${temporary_file}"
        mv "${temporary_file}" "${steam_config_file}"
    }

    set_vnc_autostart() {
        case "$1" in
            true|false) ;;
            *)
                echo 'VNC autostart must be true or false.' >&2
                return 1
                ;;
        esac

        sed -i "s|^autostart=.*$|autostart=$1|" /etc/supervisor.d/vnc.ini
    }

    configure_steam_ui_mode() {
        login_ui_mode="${STEAM_LOGIN_UI_MODE:-auto}"
        case "${login_ui_mode}" in
            auto|always|never) ;;
            *)
                echo 'STEAM_LOGIN_UI_MODE must be auto, always or never.' >&2
                return 1
                ;;
        esac

        loginusers_file="${steam_config_directory}/loginusers.vdf"
        login_ready_marker="${steam_config_directory}/l4d2-agent-ready"
        enable_vnc=true
        case "${login_ui_mode}" in
            always)
                steam_arguments=''
                ;;
            never)
                steam_arguments='-silent -no-browser'
                enable_vnc=false
                ;;
            auto)
                if { [ -f "${loginusers_file}" ] && grep -Eq '"MostRecent"[[:space:]]*"1"' "${loginusers_file}"; } || [ -f "${login_ready_marker}" ]; then
                    steam_arguments='-silent -no-browser'
                    enable_vnc=false
                else
                    steam_arguments='-vgui -no-browser'
                fi
                ;;
        esac

        sed -i "s|^command=.*$|command=/usr/games/steam ${steam_arguments}|" /etc/supervisor.d/steam.ini
        set_vnc_autostart "${enable_vnc}"
    }

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
    set_download_region
    set_l4d2_update_policy
    configure_steam_ui_mode

    sed -i 's|^autostart=.*$|autostart=true|' /etc/supervisor.d/steam.ini
fi
