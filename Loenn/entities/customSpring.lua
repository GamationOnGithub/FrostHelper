local drawableSpriteStruct = require("structs.drawable_sprite")
local jautils = require("mods").requireFromPlugin("libraries.jautils")

local springDepth = -8501
local springTexture = "objects/spring/00"

local rotations = {
    [0] = "FrostHelper/SpringFloor",
    [1] = "FrostHelper/SpringLeft",
    [2] = "FrostHelper/SpringCeiling",
    [3] = "FrostHelper/SpringRight",
}

local function getSprite(entity)
    local sprite = drawableSpriteStruct.fromTexture((entity.directory .. "00") or springTexture, entity)

    if not sprite then
        sprite = drawableSpriteStruct.fromTexture(springTexture, entity)
    end

    return sprite
end

local function createSpringHandler(name, spriteRotation, speedAsVector)
    local handler = {
        name = name,

        depth = springDepth,
        sprite = function (room, entity)
            local sprite = getSprite(entity)

            if sprite then
                sprite:setJustification(0.5, 1.0)
                sprite.rotation = spriteRotation
                local renderOutline = entity.renderOutline == nil and true or entity.renderOutline
                if renderOutline then
                    local sprites = jautils.getBorder(sprite, entity.outlineColor)
                    table.insert(sprites, sprite)
                    return sprites
                end

                return sprite
            end

            return nil
        end,
        selection = function(room, entity)
            local sprite = getSprite(entity)
            sprite:setJustification(0.5, 1.0)
            sprite.rotation = spriteRotation
            return sprite:getRectangle()
        end,
        rotate = jautils.getNameRotationHandler(rotations),
        flip = jautils.getNameFlipHandler(rotations),
        ignoredFields = { "version" }
    }

    jautils.createPlacementsPreserveOrder(handler, "normal", {
        { "color", "ffffff", "color" },
        { "directory", "objects/spring/" },
        { "speedMult", speedAsVector and "1.0" or 1.0 },
        { "attachGroup", -1, "FrostHelper.attachGroup" },
        { "version", 1, "integer" },
        { "oneUse", false },
        { "playerCanUse", true },
        { "renderOutline", true },
    })

    return handler
end

local springUp = createSpringHandler("FrostHelper/SpringFloor", 0, false)
local springRight = createSpringHandler("FrostHelper/SpringRight", -math.pi / 2, true)
local springCeiling = createSpringHandler("FrostHelper/SpringCeiling", math.pi, false)
local springLeft = createSpringHandler("FrostHelper/SpringLeft", math.pi / 2, true)

return {
    springUp,
    springRight,
    springLeft,
    springCeiling
}