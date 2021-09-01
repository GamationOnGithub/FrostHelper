module FrostHelperGrayBoosterModule

using ..Ahorn, Maple

@mapdef Entity "FrostHelper/GrayBooster" FrostHelperGrayBoosterPlacement(x::Integer, y::Integer, respawnTime::Number=1.0, particleColor::String="Gray", directory="objects/FrostHelper/grayBooster/", reappearSfx::String="event:/game/04_cliffside/greenbooster_reappear", enterSfx::String="event:/game/04_cliffside/greenbooster_enter", boostSfx::String="event:/game/04_cliffside/greenbooster_dash", releaseSfx::String="event:/game/04_cliffside/greenbooster_end", boostTime::Number=0.0)

const colors = sort(collect(keys(Ahorn.XNAColors.colors)))

const placements = Ahorn.PlacementDict(
	"Gray Booster (Frost Helper)" => Ahorn.EntityPlacement(
		FrostHelperGrayBoosterPlacement
	)
)

Ahorn.editingOptions(entity::FrostHelperGrayBoosterPlacement) = Dict{String, Any}(
    "particleColor" => colors
)

sprite = "objects/booster/booster00"

function Ahorn.selection(entity::FrostHelperGrayBoosterPlacement)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::FrostHelperGrayBoosterPlacement, room::Maple.Room)
	texture = strip(get(entity.data, "directory", "objects/FrostHelper/grayBooster/"), '/')
	realTexture = "$texture/booster00.png"
	
	Ahorn.drawSprite(ctx, realTexture, 0, 0)
end
	
end