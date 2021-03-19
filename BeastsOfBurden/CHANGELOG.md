# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.2] - 2021-03-18
### Fixed
* Cart detaches when dodging or teleporting as in vanilla Valheim

### Added
* Ability to configure which animal types can connect to cart


### Changed
* Set default cart offset for unknown animal types to be the same as used for Wolves and Boars

## [1.0.1] - 2021-03-17
### Fixed
* NullReferenceException when attaching cart to player. I wasn't able to reproduce this but I added some more aggressive null checks and cart now attaches based on Character transform not Tameable transform. Hopefully this will make it more friendly with other mods. Added additional logging around this as well. 


### Added
* This changelog :-)

## [1.0.0] - 2021-03-16
### Added
* Tamed boars and loxen can now be commanded to follow the player in the same manner as wolves in vanilla Valheim. This can be disabled via the configuration file.
* When interacting with the handles of the cart if a valid tame animal is in range the cart will attach to the nearest animal. Otherwise if the player is in a valid position the cart will attach to them.
* Animals are able to pull heavier carts than the player can, larger animals can pull heavier carts than smaller animals.
* Configuration file