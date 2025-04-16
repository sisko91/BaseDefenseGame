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
        Explosion = 3,
        Vomit = 4,
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
    public static PackedScene SelectImpactScene(this IImpactMaterial recipient, IImpactMaterial source) {
        var selected = source != null ? recipient.SelectImpactScene(source.ImpactMaterialType) : null;
        /*if(selected == null) {
            var recipientName = ((Node)recipient).Name;
            GD.PushWarning($"ImpactMaterial for {recipientName} does not include a valid response for source {source} (material type: {source?.ImpactMaterialType})");
        }*/
        return selected;
    }

    // Attempts to resolve how a specific impact material type interacts with this impact material in order to select an appropriate Impact response scene to use. May return null.
    public static PackedScene SelectImpactScene(this IImpactMaterial recipient, IImpactMaterial.MaterialType sourceType) {
        var table = recipient.ImpactResponseTable;
        if (table.TryGetValue(sourceType, out var packedFX)) {
            return packedFX;
        }
        //var recipientName = ((Node)recipient).Name;
        //GD.PushWarning($"ImpactMaterial for {recipientName} does not include a valid response for source material type: {sourceType})");
        return null;
    }

    // Attempts to register an impact against a node in the game. The node may or may not implement the necessary interfaces to facilitate a valid impact. Returns false
    // when the impact was unsuccessful (i.e. likely the recipient doesn't know how to respond).
    public static bool TryRegisterImpact(this IImpactMaterial sourceMaterial, Node recipient, HitResult hitResult, float impactDamage) {
        PackedScene impactScene = null;
        var recipientMaterial = recipient as IImpactMaterial;
        // Option 1: The receiver is also an impact material and (ideally) has a registered impact response for the source material type.
        if (recipientMaterial != null) {
            impactScene = recipientMaterial.SelectImpactScene(sourceMaterial.ImpactMaterialType);
            //GD.Print($"Using recipient for response: {impactScene?.ResourcePath}");
        }
        // Option 2: Use this material's default response scene if it exists.
        if (impactScene == null) {
            //GD.Print($"Using default response hint: {sourceMaterial?.DefaultResponseHint?.ResourcePath}");
            impactScene = sourceMaterial.DefaultResponseHint; // Default response for anything this source hits, may be null.
        }
        // Option 3: If the recipient is a valid impact material, fall back to any general default response in its table.
        if(impactScene == null && recipientMaterial != null) {
            //GD.Print($"Using recipient's default response: {impactScene?.ResourcePath}");
            impactScene = recipientMaterial.SelectImpactScene(IImpactMaterial.MaterialType.Default);
        }

        if (impactScene?.Instantiate() is Impact impact) {
            recipient.AddChild(impact);
            impact.Initialize(hitResult);
        }
        else {
            GD.PushWarning($"ImpactMaterial for {recipient.Name} does not include a valid response for source {sourceMaterial} (material type: {sourceMaterial?.ImpactMaterialType})");
        }

        // Characters receive hits on valid impacts.
        if (recipient is Character character) {
            character.ReceiveHit(hitResult, impactDamage, sourceMaterial as IInstigated);
        }

        return true;
    }
}