local logger = require("logger")
local millennium = require("millennium")
local json = require("json")
local utils = require("utils")

local function home_directory()
    return utils.getenv("USER_HOME") or utils.getenv("HOME") or "/home/default"
end

local function status_file_path()
    return utils.getenv("STEAM_DOWNLOAD_REGION_STATUS_FILE")
        or (home_directory() .. "/.steam/steam/config/steam-download-region")
end

local function parse_region_id(value)
    if value == nil or value == "" then
        return nil
    end

    local numeric = tonumber(value)
    if numeric ~= nil and numeric >= 0 and numeric <= 2147483647 and numeric == math.floor(numeric) then
        return math.floor(numeric)
    end

    local legacy = string.lower(value)
    local known = {
        cng = 47,
        shanghai = 47,
        hongkong = 33,
        qingdao = 168,
        tokyo = 32,
    }
    return known[legacy]
end

local function configured_region_id()
    local configured_home = utils.getenv("USER_HOME")
    if configured_home == nil or configured_home == "" then
        configured_home = "/home/default"
    end
    local configured_file = configured_home .. "/.config/millennium/steam-region-bridge-region"
    local configured_value = utils.read_file(configured_file)
    local has_configured_file = configured_value ~= nil
    local file_region_id = parse_region_id(configured_value and utils.trim(configured_value))
    local environment_region_id = parse_region_id(utils.getenv("STEAM_DOWNLOAD_REGION_ID"))
        or parse_region_id(utils.getenv("STEAM_DOWNLOAD_REGION"))
    local target_region_id = has_configured_file and file_region_id or environment_region_id
    logger:info(
        "Steam Region Bridge config target " .. tostring(target_region_id)
            .. " file " .. configured_file
            .. " hasFile " .. tostring(has_configured_file)
            .. " fileRegion " .. tostring(file_region_id)
            .. " environmentRegion " .. tostring(environment_region_id)
    )
    return target_region_id
end

function get_region_bridge_config(_params)
    return json.encode({
        targetRegionId = configured_region_id(),
    })
end

function record_region_state(params)
    local region_id = tonumber(params)
    if type(params) == "table" then
        region_id = tonumber(params.regionId)
    end
    if region_id == nil or region_id < 0 or region_id > 2147483647 or region_id ~= math.floor(region_id) then
        return json.encode({ success = false, error = "invalid_region_id" })
    end

    local ok, error_message = utils.write_file(status_file_path(), tostring(math.floor(region_id)) .. "\n")
    if not ok then
        logger:error("Steam Region Bridge could not persist the current region: " .. tostring(error_message))
        return json.encode({ success = false, error = "state_write_failed" })
    end

    return json.encode({ success = true, currentRegionId = math.floor(region_id) })
end

local function on_load()
    logger:info("Steam Region Bridge loaded")
    millennium.ready()
end

return {
    on_load = on_load,
}
