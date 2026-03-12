--- VehicleInfo: Extra information for each vehicle
---
--- These hooks allow specifying extra information for each vehicle (see VehicleInfoHooks.fetch). This includes marking
--- addon vehicles as such, vehicle spawn permissions, and creating custom classes for the vehicle spawner, etc.

--- @alias VehicleShortname string Shortname of a vehicle, e.g. "adder"
--- @alias VehicleList VehicleShortname[]

--- @class CustomVehicleClass
--- @field name string
--- @field vehicles VehicleList

--- @alias CustomVehicleClasses CustomVehicleClass[] List of custom vehicle classes in the order they will appear

--- @class VehicleInfo
--- @field addons VehicleList Vehicles that will be marked as addons in the vehicle spawner
--- @field blacklisted VehicleList Vehicles that will be blacklisted and only spawnable by players with the
---                                 VOVehiclesBlacklist permission
--- @field hidden VehicleList Vehicles that will be hidden in the normal list of vehicles and only spawnable by players
---                            with the VODisableFromDefaultList permission
--- @field sporty VehicleList Vehicles that can spawn when spawning a sporty vehicle
--- @field customClasses CustomVehicleClasses Custom classes that will appear in the vehicle spawner

VehicleInfoHooks = {

    --- @return VehicleInfo
    fetch = function(_)
        -- DO NOT CHANGE THE NAME OF THIS FUNCTION
        -- THE RETURN TYPE MUST FOLLOW THE SPECIFICATION

        return {
            addons = {},
            blacklisted = {},
            hidden = {},
            sporty = {},
            customClasses = {},
        }

        --[[
        Example:

        return {
            addons = {},
            blacklisted = { 'oppressor2' },
            hidden = { 'cablecar' },
            sporty = { 'adder', 'drafter', 'entity3' },
            customClasses = {
                { name = "Press F", vehicles = { 'drafter', 'ninef', 'ninef2', 'tenf', 'tenf2' } },
                { name = "Entities", vehicles = { 'entity3', 'entityxf', 'entity2' } },
            }
        }
        --]]
    end
}
