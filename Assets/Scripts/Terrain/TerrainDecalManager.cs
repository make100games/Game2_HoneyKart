using System.Collections.Generic;
using UnityEngine;

namespace HoneyKart.Terrain
{
    /// <summary>
    /// Scene singleton that aggregates up to 8 active TerrainDecal instances and
    /// pushes all required shader globals every LateUpdate. Supports in-editor
    /// live preview via the Refresh() method called from TerrainDecal.OnValidate.
    /// </summary>
    public class TerrainDecalManager : MonoBehaviour
    {
        public const int MaxDecals = 8;

        private static TerrainDecalManager _instance;

        /// <summary>Lazy singleton resolved via FindFirstObjectByType and cached.</summary>
        public static TerrainDecalManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<TerrainDecalManager>();
                return _instance;
            }
        }

        private readonly List<TerrainDecal> _decals = new List<TerrainDecal>(MaxDecals);

        // Shader property IDs cached in the static constructor to avoid per-frame string hashing.
        private static readonly int[] AlbedoTexIds = new int[MaxDecals];
        private static readonly int[] NormalTexIds  = new int[MaxDecals];
        private static readonly int[] DataAIds      = new int[MaxDecals];
        private static readonly int[] DataBIds      = new int[MaxDecals];

        private static readonly int OpacitiesId       = Shader.PropertyToID("_TerrainDecalOpacities");
        private static readonly int Opacities2Id      = Shader.PropertyToID("_TerrainDecalOpacities2");
        private static readonly int NormalStrengthsId  = Shader.PropertyToID("_TerrainDecalNormalStrengths");
        private static readonly int NormalStrengths2Id = Shader.PropertyToID("_TerrainDecalNormalStrengths2");
        private static readonly int CountId            = Shader.PropertyToID("_TerrainDecalCount");

        static TerrainDecalManager()
        {
            for (int i = 0; i < MaxDecals; i++)
            {
                AlbedoTexIds[i] = Shader.PropertyToID($"_TerrainDecalTex{i}");
                NormalTexIds[i] = Shader.PropertyToID($"_TerrainDecalNorm{i}");
                DataAIds[i]     = Shader.PropertyToID($"_TerrainDecalA{i}");
                DataBIds[i]     = Shader.PropertyToID($"_TerrainDecalB{i}");
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Initialize all 8 slots to no-op defaults so the shader always has
            // valid textures bound even before any decal registers.
            for (int i = 0; i < MaxDecals; i++)
            {
                Shader.SetGlobalTexture(AlbedoTexIds[i], Texture2D.blackTexture);
                Shader.SetGlobalTexture(NormalTexIds[i],  Texture2D.normalTexture);
            }
        }

        /// <summary>Adds a decal to the active list. Logs a warning if over capacity.</summary>
        public void Register(TerrainDecal decal)
        {
            if (_decals.Contains(decal))
                return;

            if (_decals.Count >= MaxDecals)
            {
                Debug.LogWarning(
                    $"[TerrainDecalManager] Cannot register '{decal.name}': " +
                    $"maximum of {MaxDecals} decals reached.", decal);
                return;
            }

            _decals.Add(decal);
        }

        /// <summary>Removes a decal from the active list.</summary>
        public void Unregister(TerrainDecal decal)
        {
            _decals.Remove(decal);
        }

        /// <summary>
        /// Manually triggers a globals push. Called from TerrainDecal.OnValidate
        /// so that inspector changes update the terrain in real-time in the Editor.
        /// </summary>
        public void Refresh()
        {
            PushGlobals();
        }

        private void LateUpdate()
        {
            PushGlobals();
        }

        private void PushGlobals()
        {
            int activeCount = _decals.Count;

            Vector4 op1 = Vector4.zero;
            Vector4 op2 = Vector4.zero;
            Vector4 ns1 = Vector4.zero;
            Vector4 ns2 = Vector4.zero;

            for (int i = 0; i < MaxDecals; i++)
            {
                bool active = i < activeCount;

                if (active)
                {
                    TerrainDecal d = _decals[i];
                    Shader.SetGlobalTexture(AlbedoTexIds[i], d.DecalTexture != null ? d.DecalTexture : Texture2D.blackTexture);
                    Shader.SetGlobalTexture(NormalTexIds[i],  d.NormalMap    != null ? d.NormalMap    : Texture2D.normalTexture);
                    Shader.SetGlobalVector(DataAIds[i], d.DataA);
                    Shader.SetGlobalVector(DataBIds[i], d.DataB);
                }
                else
                {
                    // Zero-out inactive data vectors so the shader masks them out.
                    Shader.SetGlobalVector(DataAIds[i], Vector4.zero);
                    Shader.SetGlobalVector(DataBIds[i], Vector4.zero);
                }

                float opacity       = active ? _decals[i].Opacity       : 0f;
                float normalStrength = active ? _decals[i].NormalStrength : 0f;

                switch (i)
                {
                    case 0: op1.x = opacity; ns1.x = normalStrength; break;
                    case 1: op1.y = opacity; ns1.y = normalStrength; break;
                    case 2: op1.z = opacity; ns1.z = normalStrength; break;
                    case 3: op1.w = opacity; ns1.w = normalStrength; break;
                    case 4: op2.x = opacity; ns2.x = normalStrength; break;
                    case 5: op2.y = opacity; ns2.y = normalStrength; break;
                    case 6: op2.z = opacity; ns2.z = normalStrength; break;
                    case 7: op2.w = opacity; ns2.w = normalStrength; break;
                }
            }

            Shader.SetGlobalVector(OpacitiesId,       op1);
            Shader.SetGlobalVector(Opacities2Id,      op2);
            Shader.SetGlobalVector(NormalStrengthsId,  ns1);
            Shader.SetGlobalVector(NormalStrengths2Id, ns2);
            Shader.SetGlobalFloat(CountId, activeCount);
        }
    }
}
