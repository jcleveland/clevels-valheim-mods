# Beasts of Burden
A mod that allows you to attach carts to tamed boars, wolves, and loxen. 

## Features:
* Tamed boars and loxen can now be commanded to follow the player in the same manner as wolves in vanilla Valheim. This can be disabled via the configuration file.
* When interacting with the handles of the cart if a valid tame animal is in range the cart will attach to the nearest animal. Otherwise if the player is in a valid position the cart will attach to them.
* Animals are able to pull heavier carts than the player can, larger animals can pull heavier carts than smaller animals.

## Configuration:
After launching the game with the mod installed for the first time a configuration file will be created in `SteamApps\common\Valheim\BepInEx\config\org.bepinex.plugins.beasts_of_burden.cfg`
In the configuration you can adjust things such as:
* Which animal types you can command to follow you.
* How closely an animal will attempt to follow.
* How close the animal or player have to be to the cart to attach to it.
* Which animals you can attach to a cart.
* If tamed animals should ignore their fear of fire.

## Installation
Like many Valheim mods Beasts of Burden relies on BepinEx and Harmony mod. For instructions on installing such mods see: https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/ or https://www.nexusmods.com/valheim/mods/15

## Known Issues
* If a player leaves the area (e.g., teleporting or quitting) after attaching a cart to an animal the cart will detach even if other players are still in the area.
* Cart connection can be a bit jostling if detach distance setting is too high.

## Known mod compatibility issues
* Doesn't curently work with Linked Wagons 