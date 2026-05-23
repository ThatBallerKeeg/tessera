namespace Engine.Runtime.Entities;

/// <summary>
/// Base class for all engine and user-scripted components.
/// Subclasses override only the lifecycle methods they need.
/// </summary>
/// <remarks>
/// Lifecycle order per GameObject:
/// <list type="number">
///   <item><description><see cref="OnAttach"/> — called once when added to a GameObject.</description></item>
///   <item><description><see cref="OnUpdate"/> — called every simulation tick.</description></item>
///   <item><description><see cref="OnDraw"/>   — called every render frame (Y-sorted pass).</description></item>
///   <item><description><see cref="OnDetach"/> — called once when removed from a GameObject.</description></item>
/// </list>
/// </remarks>
public abstract class Component
{
    /// <summary>The GameObject this component is attached to. Null before <see cref="OnAttach"/>.</summary>
    public GameObject? Owner { get; internal set; }

    /// <summary>
    /// Called immediately after this component is added to <paramref name="owner"/>.
    /// Use to cache sibling-component references (prefer lazy lookup in <see cref="OnUpdate"/>
    /// to avoid ordering issues).
    /// </summary>
    public virtual void OnAttach(GameObject owner) { }

    /// <summary>
    /// Called once per simulation tick.
    /// <paramref name="deltaSeconds"/> is wall-clock elapsed time since the last tick.
    /// </summary>
    public virtual void OnUpdate(float deltaSeconds) { }

    /// <summary>
    /// Called once per render frame, after all tilemap layers are drawn.
    /// Components are drawn in <see cref="GameObject.Position"/>.Y-ascending order.
    /// </summary>
    public virtual void OnDraw(ISpriteBatch spriteBatch) { }

    /// <summary>Called immediately before this component is removed from its owner.</summary>
    public virtual void OnDetach() { }
}
