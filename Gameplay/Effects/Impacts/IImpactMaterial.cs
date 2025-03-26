using Godot;
using Godot.Collections;

// IImpactMaterial provides a description of a material involved in impacts. This could be used to determine things like:
// - Footstep sounds to play for different walking surfaces.
// - Impact FX to play for different projectile collisions (or speeds).
public interface IImpactMaterial
{
    // All material types defined for impact resolution. Add to this list as needed.
    public enum MaterialType
    {
        Default = 0, // Pseudo-material, should not be assigned to objects or returned from ImpactMaterialType. This is only used in the editor and for internal lookups.
        Human = 1,
        Bullet = 2,
    }

    // Implementations must provide a MaterialType identifier.
    public MaterialType ImpactMaterialType { get; }

    // When this material impacts something that is not an impact material itself, this hint can be used as the recommended response.
    public PackedScene DefaultResponseHint { get; }

    // Implementations must provide an ImpactResponseTable for determining appropriate FX during impact resolution.
    public Dictionary<MaterialType, PackedScene> ImpactResponseTable { get; }
}

public static class IImpactMaterialExtensions
{
    // Attempts to resolve two impacting materials using their exposed interfaces to select an appropriate Impact response scene to use. May return null.
    public static PackedScene SelectImpactScene(this IImpactMaterial recipient, IImpactMaterial source, bool useDefaultAsFallback = true) {
        var table = recipient.ImpactResponseTable;
        // Option 1: Impact FX registered for this source and recipient.
        if (source != null && table.TryGetValue(source.ImpactMaterialType, out var packedFX)) {
            return packedFX;
        }
        else if (useDefaultAsFallback) {
            // Option 2: Impact FX registered for the Default source and this recipient.
            if (table.TryGetValue(IImpactMaterial.MaterialType.Default, out var defaultSourceFX)) {
                return defaultSourceFX;
            }
        }
        var recipientName = ((Node)recipient).Name;
        GD.PushWarning($"ImpactMaterial for {recipientName} does not include a valid response for source {source} (material type: {source?.ImpactMaterialType})");
        return null;
    }

    // Attempts to register an impact against a node in the game. The node may or may not implement the necessary interfaces to facilitate a valid impact. Returns false
    // when the impact was unsuccessful (i.e. likely the recipient doesn't know how to respond).
    public static bool TryRegisterImpact(this Node recipient, HitResult hitResult, IImpactMaterial sourceMaterial, float impactDamage) {
        bool bDidImpact = false;
        PackedScene impactScene = null;
        if(recipient is IImpactMaterial recipientMaterial) {
            impactScene = recipientMaterial.SelectImpactScene(sourceMaterial);
            //GD.Print($"Using recipient for response: {impactScene?.ResourcePath}");
        }
        else {
            //GD.Print($"Using default response hint: {sourceMaterial?.DefaultResponseHint?.ResourcePath}");
            impactScene = sourceMaterial.DefaultResponseHint; // Default response for anything this source hits, may be null.
        }

        if (impactScene?.Instantiate() is Impact impact) {
            bDidImpact = true;
            recipient.AddChild(impact);
            impact.GlobalPosition = hitResult.ImpactLocation;
            impact.GlobalRotation = (hitResult.ImpactNormal * -1).Angle(); // reverse the normal as it will point inward toward the character hit.
        }

        // Characters receive hits on valid impacts.
        if (bDidImpact && recipient is Character character) {
            character.ReceiveHit(hitResult, impactDamage, sourceMaterial as IInstigated);
        }

        return bDidImpact;
    }
}