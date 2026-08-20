local logger = require("logger")
local millennium = require("millennium")
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
    local configured_file = home_directory() .. "/.config/millennium/steam-region-bridge-region"
    local configured_value = utils.read_file(configured_file)
    return parse_region_id(configured_value and utils.trim(configured_value))
        or parse_region_id(utils.getenv("STEAM_DOWNLOAD_REGION_ID"))
        or parse_region_id(utils.getenv("STEAM_DOWNLOAD_REGION"))
end

function get_region_bridge_config(_params)
    return {
        targetRegionId = configured_region_id(),
    }
end

function record_region_state(params)
    local region_id = tonumber(params)
    if type(params) == "table" then
        region_id = tonumber(params.regionId)
    end
    if region_id == nil or region_id < 0 or region_id > 2147483647 or region_id ~= math.floor(region_id) then
        return { success = false, error = "invalid_region_id" }
    end

    local ok, error_message = utils.write_file(status_file_path(), tostring(math.floor(region_id)) .. "\n")
    if not ok then
        logger:error("Steam Region Bridge could not persist the current region: " .. tostring(error_message))
        return { success = false, error = "state_write_failed" }
    end

    return { success = true, currentRegionId = math.floor(region_id) }
end

local function on_load()
    logger:info("Steam Region Bridge loaded")
    millennium.ready()
end

return {
    on_load = on_load,
}
