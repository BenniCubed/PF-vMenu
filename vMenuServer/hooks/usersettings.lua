--- Usersettings: Server-specific settings for each user, changeable in vMenu.
---
--- These hooks allow specifying which settings should be available on the server (see UsersettingsHooks.getInfo), as
--- well as how the settings are persisted for a user (see UsersettingsHooks.fetchAllFor and
--- UsersettingsHooks.storeFor).
---
--- In addition to these hooks, vMenu will also trigger events when a usersetting is updated and exposes event handlers
--- for updating usersettings from other resources:
---
---  Events sent from vMenu:
---
---   - vMenu:UsersettingUpdated(key: string, value: SettingValue)
---   - vMenu:UsersettingUpdated:<key>(value: SettingValue)
---
---     Sent whenever the usersetting with the specified key is updated to a new value. The first version allows you to
---     listen to updates of all usersettings, while the latter is useful if you are only interested in a specific
---     usersetting.
---
---   - vMenu:GetUsersettingResponse({ key: string, value: SettingValue?, requestId: number?, error: string? })
---
---     Sent in response to vMenu:GetUsersetting. The passed table contains the key and value of the usersetting, as
---     well as the requestId if it was specified. If an invalid usersetting's value was requested, value is null and
---     error is provided.
---
---  Events handled by vMenu:
---
---   - vMenu:UpdateUsersetting(key: string, value: SettingValue, sync: boolean):
---
---     Updates the usersetting with the given key to hold the given value (if valid), and updates the corresponding
---     item in the usersettings menu. sync determines whether vMenu will sync the value to the server or not (default
---     is false). This is useful if the updating resource takes care of updating the value on the server itself.
---
---   - vMenu:GetUsersetting(key: string, requestId: number?)
---
---     Requests vMenu to provide the value of the usersetting with the specified key. In response, vMenu will trigger
---     the vMenu:GetUsersettingResponse event. If the optional requestId is specified, it will be included in the
---     response to make it allow matching the request to a particular response.

--- Type that determines the concrete UsersettingSpec.spec of a usersetting
--- @alias UsersettingSpecType
--- | '"list"'
--- | '"range"'
--- | '"toggle"'

--- @alias UsersettingListSpecItemKey (boolean | number | string )

--- @class UsersettingListSpecItem
--- @field key UsersettingListSpecItemKey -- The item's key (for storage and update events)
--- @field name string                    -- The (display) name of the item

--- UsersettingSpec.spec class for UsersettingSpec.type == "list"
--- @class UsersettingListSpec
--- @field items UsersettingListSpecItem[]       -- List of items. All items must have distinct keys, and all keys must
---                                              -- be of the same type
--- @field defaultKey UsersettingListSpecItemKey -- The default key (if none is stored yet)

--- UsersettingSpec.spec class for UsersettingSpec.type == "range"
--- @class UsersettingRangeSpec
--- @field begin integer        -- First value it the range
--- @field end integer          -- The last value in the range (inclusive, must be reachable from begin based on step)
--- @field step integer         -- The step size (>= 1)
--- @field defaultValue integer -- The default value (if none is stored yet)

--- UsersettingSpec.spec class for UsersettingSpec.type == "toggle"
--- @class UsersettingToggleSpec
--- @field defaultState boolean -- The default state (if none is stored yet)

--- @class UsersettingSpec
--- @field key string               -- The key of the usersetting (for storage and update events)
--- @field name string              -- The (display) name of the usersetting
--- @field description string       -- The (display) description of the usersetting
--- @field type UsersettingSpecType -- The type of the usersetting, determines the concrete spec below
--- @field spec (UsersettingListSpec | UsersettingRangeSpec | UsersettingToggleSpec) -- Concrete usersetting spec, based
---                                                                                  -- on the type above

--- @alias UsersettingsSpecs UsersettingSpec[] -- The list of usersetting specs (in the order they will be displayed)

--- @class UsersettingsInfo
--- @field menuName string         -- Display name of the usersettings menu
--- @field menuDescription string  -- Description of the usersettings menu
--- @field specs UsersettingsSpecs -- The list of usersetting specs (in the order they will be displayed)

--- @alias PlayerSrc string  -- Player handle (as a string)
--- @alias SettingKey string -- Key of a usersetting (corresponds to a concrete UsersettingSpec.key)
--- @alias SettingValue (boolean | number | string) -- Value of a usersetting
--- @alias PlayerSettings table<SettingKey, SettingValue> -- Table of concrete usersetting-value mappings

--- List and set of columns
local usersettingsColumns = nil

--- @param playerSrc PlayerSrc
local function getLicense(playerSrc)
    return GetPlayerIdentifierByType(playerSrc, "license")
end

local function getUsersettingsColumns(specs)
    local columns = {}
    for _, spec in ipairs(specs) do
        table.insert(columns, spec.key)
        columns[spec.key] = true
    end
    return columns
end

UsersettingsHooks = {

    --- Get the information that describes the usersettings menu
    --- @return UsersettingsInfo
    getInfo = function()
        -- DO NOT CHANGE THE NAME OF THIS FUNCTION
        -- THE RETURN TYPE MUST FOLLOW THE SPECIFICATION

        -- TODO :)
        local specs = {}

        usersettingsColumns = getUsersettingsColumns(specs)

        return {
            menuName = "User Settings",
            specs = specs,
        }
    end,

    --- Fetch all usersettings for the given player
    --- @param playerSrc PlayerSrc
    --- @return PlayerSettings
    fetchAllFor = function(playerSrc)
        -- DO NOT CHANGE THE NAME OF THIS FUNCTION
        -- THE RETURN TYPE MUST FOLLOW THE SPECIFICATION

        -- The __dummy field exists solely so we encode the returned value as a JSON object instead of an array
        local dummy = { __dummy = true }

        assert(usersettingsColumns)

        if #usersettingsColumns == 0 then
            return dummy
        end

        local license = getLicense(playerSrc)

        -- TODO :)

        return dummy
    end,

    --- Store the given usersettings for the given player
    --- @param storeData { player: PlayerSrc, settings: PlayerSettings }
    storeFor = function(storeData)
        -- DO NOT CHANGE THE NAME OF THIS FUNCTION
        -- THE RETURN TYPE MUST FOLLOW THE SPECIFICATION

        assert(usersettingsColumns)

        if #usersettingsColumns == 0 then
            return
        end

        local playerSrc = storeData.player
        local settings = storeData.settings

        local license = getLicense(playerSrc)

        -- TODO :)
    end
}
