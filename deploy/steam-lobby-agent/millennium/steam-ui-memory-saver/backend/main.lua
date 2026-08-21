local logger = require("logger")
local millennium = require("millennium")
local json = require("json")
local utils = require("utils")

local function home_directory()
    return utils.getenv("USER_HOME") or utils.getenv("HOME") or "/home/default"
end

local function login_ui_mode()
    return string.lower(utils.getenv("STEAM_LOGIN_UI_MODE") or "auto")
end

local function readiness_marker_path()
    return home_directory() .. "/.steam/steam/config/l4d2-agent-ready"
end

local function no_vnc_ready()
    local mode = login_ui_mode()
    if mode == "never" then
        return true
    end
    if mode ~= "auto" then
        return false
    end

    local marker = utils.read_file(readiness_marker_path())
    return marker ~= nil and utils.trim(marker) == "ready"
end

function get_steam_ui_memory_saver_config(_params)
    local mode = login_ui_mode()
    local marker_path = readiness_marker_path()
    local enabled = no_vnc_ready()
    logger:info(
        "Steam UI Memory Saver mode " .. mode
            .. " enabled " .. tostring(enabled)
            .. " marker " .. marker_path
    )
    return json.encode({
        enabled = enabled,
        mode = mode,
        marker = marker_path,
    })
end

local function on_load()
    logger:info("Steam UI Memory Saver loaded")
    millennium.ready()
end

return {
    on_load = on_load,
}
