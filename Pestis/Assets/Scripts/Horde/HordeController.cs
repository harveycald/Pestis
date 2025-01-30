using System;
using System.Collections.Generic;
using Fusion;
using JetBrains.Annotations;
using Players;
using POI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Horde
{
    public class HordeController : NetworkBehaviour
    {
        public Player Player;

        public GameObject ratPrefab;

        // Do not use or edit yourself, used to expose internals to Editor
        [SerializeField] private int devToolsTotalRats;
        [SerializeField] private float devToolsTotalHealth;
        [SerializeField] private Vector2 devToolsTargetLocation;

        /// <summary>
        ///     Location rats are trying to get to, synced across network
        /// </summary>
        public NetworkTransform targetLocation;

        /// <summary>
        ///     Distance a rat must get to its current intra-horde target to move onto the next one
        /// </summary>
        public float targetTolerance;

        /// <summary>
        ///     Points in the horde that individual rats will cycle between moving towards, to create continuous movement
        /// </summary>
        public Vector2[] intraHordeTargets = new Vector2[4];

        /// <summary>
        ///     The horde we're currently damaging. Our rats will animate against them.
        /// </summary>
        [SerializeField] internal HordeController HordeBeingDamaged;

        private readonly List<RatController> _spawnedRats = new();

        /// <summary>
        ///     Mid-point of all the rats in the horde
        /// </summary>
        private Vector2 _hordeCenter;

        private PopulationController _populationController;

        /// <summary>
        ///     How many rats we need to spawn to have the correct amount visible.
        /// </summary>
        private int _ratsToSpawn;

        private Light2D _selectionLightPOI;

        private Light2D _selectionLightTerrain;

        public int AliveRats => (int)Mathf.Max(TotalHealth / _populationController.GetState().HealthPerRat, 1.0f);

        [Networked]
        [OnChangedRender(nameof(TotalHealthChanged))]
        internal float TotalHealth { get; set; }

        /// <summary>
        ///     Bounds containing every rat in Horde
        /// </summary>
        [Networked]
        private Bounds _hordeBounds { set; get; }

        [Networked] [CanBeNull] public POIController StationedAt { get; private set; }

        private void Awake()
        {
            _hordeCenter = transform.position;
        }

        private void FixedUpdate()
        {
            if (_spawnedRats.Count == 0) _hordeCenter = transform.position;

            // Only spawn up to one rat each tick to avoid freezes
            if (_ratsToSpawn != 0)
            {
                // Spawn a Rat
                var rat = Instantiate(ratPrefab, _hordeCenter, Quaternion.identity, transform);
                var ratController = rat.GetComponent<RatController>();
                ratController.SetHordeController(this);
                ratController.Start();
                _spawnedRats.Add(ratController);
                _ratsToSpawn--;
            }

            if (Player.InCombat && Player.GetCombatController().HordeInCombat(this))
            {
                var combat = Player.GetCombatController();
                var enemy = combat.GetNearestEnemy(this);

                // If we chose to be in combat, move towards enemy
                if (combat.HordeIsVoluntary(this))
                    // Teleports target, not us
                    targetLocation.Teleport(enemy.GetBounds().center);

                // If close enough, start dealing damage, and animating rats.
                if (enemy.GetBounds().Intersects(_hordeBounds))
                {
                    enemy.DealDamageRpc(_populationController.GetState().Damage);
                    HordeBeingDamaged = enemy;
                }
                else
                {
                    HordeBeingDamaged = null;
                }
            }

            // Can't calculate the bounds of nothing
            if (_spawnedRats.Count == 0) return;


            devToolsTargetLocation = targetLocation.transform.position;

            // Calculate bounding box that contains all rats
            var b = new Bounds(_spawnedRats[0].transform.position, Vector2.zero);
            foreach (var rat in _spawnedRats) b.Encapsulate(rat.GetPosition());

            b.Expand(1.0f);

            // If we're the owner of this Horde, we are the authoritative source for the horde bounds
            if (HasStateAuthority) _hordeBounds = b;

            _selectionLightTerrain.pointLightInnerRadius = b.extents.magnitude * 0.9f + 0.5f;
            _selectionLightTerrain.pointLightOuterRadius = b.extents.magnitude * 1.0f + 0.5f;
            _selectionLightTerrain.transform.position = b.center;


            _selectionLightPOI.pointLightInnerRadius = b.extents.magnitude * 0.9f + 0.5f;
            _selectionLightPOI.pointLightOuterRadius = b.extents.magnitude * 1.0f + 0.5f;
            _selectionLightPOI.transform.position = b.center;

            intraHordeTargets[0] = new Vector2(targetLocation.transform.position.x - b.extents.x * 0.65f,
                targetLocation.transform.position.y + b.extents.y * 0.65f);
            intraHordeTargets[1] = new Vector2(targetLocation.transform.position.x - b.extents.x * 0.65f,
                targetLocation.transform.position.y - b.extents.y * 0.65f);
            intraHordeTargets[2] = new Vector2(targetLocation.transform.position.x + b.extents.x * 0.65f,
                targetLocation.transform.position.y - b.extents.y * 0.65f);
            intraHordeTargets[3] = new Vector2(targetLocation.transform.position.x + b.extents.x * 0.65f,
                targetLocation.transform.position.y + b.extents.y * 0.65f);
            targetTolerance = b.extents.magnitude * 0.1f;

            _hordeCenter = b.center;
        }

#if UNITY_EDITOR
        [DrawGizmo(GizmoType.Selected ^ GizmoType.NonSelected)]
        public void OnDrawGizmos()
        {
            if (Object.LastReceiveTick)
            {
                var centeredStyle = GUI.skin.GetStyle("Label");
                centeredStyle.alignment = TextAnchor.MiddleCenter;

                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(_hordeBounds.center, _hordeBounds.size);
                Handles.Label(_hordeBounds.center, $@"{Object.StateAuthority}
{Object.Id}
{(HasStateAuthority ? "Local" : "Remote")}
Combat: {(Player.InCombat ? Player.GetCombatController().HordeInCombat(this) : "None")}
Horde Target: {(HordeBeingDamaged ? HordeBeingDamaged.Object.Id : "None")}
");
                // Handles.Label(_hordeBounds.center, $"{Object.StateAuthority}");
                // Handles.Label(_hordeBounds.center + new Vector3(0, -0.5f, 0), $"{Object.Id}");
                // if (HasStateAuthority) Handles.Label(_hordeBounds.center + new Vector3(0, -0.75f, 0), "Local");
                // if (HordeBeingDamaged)
                //     Handles.Label(_hordeBounds.center + new Vector3(0, -1, 0),
                //         $"Fighting {HordeBeingDamaged.Object.Id}");
                HandleUtility.Repaint();
            }
        }
#endif

        // When inspector values change, update appropriate variables
        public void OnValidate()
        {
            if (!Application.isPlaying) return;

            // Don't allow changes, or it'll keep overwriting
            if (Player.InCombat && Player.GetCombatController().HordeInCombat(this)) return;

            TotalHealth = _populationController.GetState().HealthPerRat * devToolsTotalRats;
            targetLocation.transform.position = devToolsTargetLocation;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void StationAtRpc(POIController poi)
        {
            poi.StationHordeRpc(this);
            targetLocation.Teleport(poi.transform.position);
            StationedAt = poi;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UnStationAtRpc()
        {
            if (!StationedAt) throw new Exception("Tried to unstation, but not stationed anywhere!");
            StationedAt.UnStationHordeRpc(this);
            StationedAt = null;
        }


        /// <summary>
        ///     Update number of visible rats based on current health
        /// </summary>
        internal void TotalHealthChanged()
        {
            // Update values shown in inspector
            devToolsTotalRats = AliveRats;
            devToolsTotalHealth = TotalHealth;

            if (AliveRats < 0)
                // Initial value is bad
                return;

            var difference = AliveRats - _spawnedRats.Count;
            if (difference > 0)
                _ratsToSpawn = difference;
            else if (difference < 0)
                // Kill a Rat
                for (var i = 0; i > difference; i--)
                {
                    Destroy(_spawnedRats[_spawnedRats.Count - 1 + i].transform.gameObject);
                    _spawnedRats.RemoveAt(_spawnedRats.Count - 1 + i);
                }
        }

        public override void Spawned()
        {
            _populationController = GetComponent<PopulationController>();
            Player = GetComponentInParent<Player>();

            _selectionLightTerrain = transform.Find("SelectionLightTerrain").gameObject.GetComponent<Light2D>();
            _selectionLightPOI = transform.Find("SelectionLightPOI").gameObject.GetComponent<Light2D>();
            if (!HasStateAuthority)
            {
                _selectionLightPOI.color = Color.red;
                _selectionLightTerrain.color = Color.red;
            }

            targetLocation = transform.Find("TargetLocation").gameObject.GetComponent<NetworkTransform>();

            // Needed to spawn in rats from joined session
            TotalHealthChanged();
        }


        public void Highlight()
        {
            _selectionLightTerrain.enabled = true;
            _selectionLightPOI.enabled = true;
        }

        public void UnHighlight()
        {
            _selectionLightTerrain.enabled = false;
            _selectionLightPOI.enabled = false;
        }

        public void Move(Vector2 target)
        {
            targetLocation.Teleport(target);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DealDamageRpc(float damage)
        {
            TotalHealth -= damage;
        }

        public Bounds GetBounds()
        {
            return _hordeBounds;
        }

        public RatController ClosestRat(Vector2 pos)
        {
            RatController bestTarget = null;
            var closestDistance = Mathf.Infinity;

            foreach (var rat in _spawnedRats)
            {
                var dist = ((Vector2)rat.transform.position - pos).sqrMagnitude;

                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestTarget = rat;
                }
            }

            return bestTarget;
        }

        /// <summary>
        ///     Run for your furry little lives to the nearest friendly POI
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RetreatRpc()
        {
            // For now just retreat to spawn base
            targetLocation.Teleport(transform.parent.position);
            HordeBeingDamaged = null;
        }

        public void AttackPoi(POIController poi)
        {
            Debug.Log("Attacking POI");

            // TODO - Don't immediately take control just because it's unoccupied, need to find a way to wait until moved.
            if (!poi.ControlledBy)
            {
                poi.ChangeControllerRpc(Player);
                StationAtRpc(poi);
                return;
            }

            // Add current horde to new battle
            Player.JoinHordeToCombat(this);

            Player.GetCombatController().SetFightingOver(poi);

            // Add each enemy horde stationed at target POI to combat
            foreach (var horde in poi.StationedHordes)
            {
                Debug.Log("Adding enemy horde to combat!");
                Player.JoinHordeToCombat(horde);
            }
        }
    }
}