using UnityEngine;

namespace HoneyKart.Terrain
{
    /// <summary>
    /// Defines a world-space projected decal baked into the terrain shader.
    /// The transform's position XZ is the world center; transform.right and
    /// transform.up define the U/V projection axes (with the standard 90° X rotation
    /// these map correctly onto the XZ plane).
    /// </summary>
    public class TerrainDecal : MonoBehaviour
    {
        /// <summary>Albedo texture to project onto the terrain.</summary>
        [SerializeField] public Texture2D DecalTexture;

        /// <summary>
        /// Optional per-decal normal map. Leave null for decals that do not need
        /// normal contribution (e.g. finish line, logo decals).
        /// </summary>
        [SerializeField] public Texture2D NormalMap;

        /// <summary>World-space width (U) and height (V) in units.</summary>
        [SerializeField] public Vector2 Size = Vector2.one;

        /// <summary>Per-decal albedo blend strength.</summary>
        [SerializeField, Range(0f, 1f)] public float Opacity = 1f;

        /// <summary>
        /// Per-decal normal blend strength. Set to 0 for decals that have no
        /// normal map to avoid any normal contribution.
        /// </summary>
        [SerializeField, Range(0f, 1f)] public float NormalStrength = 1f;

        /// <summary>
        /// Packed center XZ + U-axis XZ for the shader.
        /// float4(pos.x, pos.z, right.x, right.z)
        /// </summary>
        internal Vector4 DataA => new Vector4(
            transform.position.x,
            transform.position.z,
            transform.right.x,
            transform.right.z
        );

        /// <summary>
        /// Packed V-axis XZ + world-space size for the shader.
        /// float4(up.x, up.z, Size.x, Size.y)
        /// </summary>
        internal Vector4 DataB => new Vector4(
            transform.up.x,
            transform.up.z,
            Size.x,
            Size.y
        );

        private void OnEnable()
        {
            TerrainDecalManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            TerrainDecalManager.Instance?.Unregister(this);
        }

        private void OnValidate()
        {
            TerrainDecalManager.Instance?.Refresh();
        }
    }
}
