local jautils = require("mods").requireFromPlugin("libraries.jautils", "FrostHelper")
local drawableLineStruct = require("structs.drawable_line")
local drawableFunction = require("structs.drawable_function")
local utils = require("utils")
local drawing = require("utils.drawing")

local lightning = {
    name = "FrostHelper/ArbitraryShapeLightning",
    nodeLimits = { 2, 999 },
    depth = -math.huge + 6
}

jautils.createPlacementsPreserveOrder(lightning, "default", {
    { "fill", true },
})

local function point(position, color)
    return jautils.getFilledRectangleSprite(utils.rectangle(position.x - 1, position.y - 1, 3, 3), color)
end

local nodeColor = jautils.getColor("ffffff")
local lineColor = jautils.getColor("fcf579")
local fillColor = jautils.getColor("fcf57919")

local function drawFilledPolygon(pt)
    drawing.callKeepOriginalColor(function()
        love.graphics.setColor(fillColor)
        local ok, triangles = pcall(love.math.triangulate, pt)
        if not ok then return end
        for _, triangle in ipairs(triangles) do
            love.graphics.polygon("fill", triangle)
        end
    end)
end

function lightning.sprite(room, entity)
    if entity.nodes then
        local points = { entity.x, entity.y }
        local nodeSprites = { point(entity, nodeColor)}
        for _, value in ipairs(entity.nodes) do
            table.insert(points, value.x)
            table.insert(points, value.y)

            table.insert(nodeSprites, point(value, nodeColor))
        end

        local filled = entity.fill ~= false

        if filled then
            table.insert(points, entity.x)
            table.insert(points, entity.y)
        end

        return jautils.union(
            filled and drawableFunction.fromFunction(drawFilledPolygon, points),
            drawableLineStruct.fromPoints(points, lineColor, 1),
            nodeSprites
        )
    end
    return jautils.getPixelSprite(entity.x, entity.y, nodeColor)
end

-- make sure nodes aren't drawn because it looks stupid
function lightning.nodeSprite() end

function lightning.selection(room, entity)
    local main = utils.rectangle(entity.x, entity.y, 4, 4)

    if entity.nodes then
        local nodeSelections = {}
        for _, node in ipairs(entity.nodes) do
            table.insert(nodeSelections, utils.rectangle(node.x, node.y, 4, 4))
        end
        return main, nodeSelections
    end

    return main, { }
end


return lightning