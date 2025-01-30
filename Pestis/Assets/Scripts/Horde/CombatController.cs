using System.Collections.Generic;
using System.Linq;
using Fusion;
using Players;
using UnityEngine;

namespace Horde
{
    /// <summary>
    ///     https://doc.photonengine.com/fusion/current/manual/fusion-types/network-collections#usage-in-inetworkstructs
    /// </summary>
    public struct CombatParticipant : INetworkStruct
    {
        public Player Player;

        [Networked] [Capacity(5)] public NetworkLinkedList<NetworkBehaviourId> Hordes => default;


        /// <summary>
        ///     Whether each horde chose to be in combat, or was attacked.
        /// </summary>
        [Networked]
        [Capacity(5)]
        public NetworkDictionary<NetworkBehaviourId, bool> Voluntary => default;

        /// <summary>
        ///     The health each horde had when it joined the battle. It will retreat if below 20%.
        /// </summary>
        [Networked]
        [Capacity(5)]
        public NetworkDictionary<NetworkBehaviourId, float> HordeStartingHealth => default;

        public CombatParticipant(Player player, HordeController hordeController, bool voluntary)
        {
            Player = player;
            Hordes.Add(hordeController);
            Voluntary.Add(hordeController, voluntary);
            HordeStartingHealth.Add(hordeController, hordeController.TotalHealth);
        }

        public void AddHorde(HordeController horde, bool voluntary)
        {
            Hordes.Add(horde);
            Voluntary.Add(horde, voluntary);
            HordeStartingHealth.Add(horde, horde.TotalHealth);
        }

        public void RemoveHorde(HordeController horde)
        {
            Hordes.Remove(horde);
            Voluntary.Remove(horde);
            HordeStartingHealth.Remove(horde);
        }
    }

    public class CombatController : NetworkBehaviour
    {
        public const int MaxParticipants = 6;

        [Networked] private Player InitiatingPlayer { get; set; }

        /// <summary>
        ///     Stores the involved players and a list of their HordeControllers (as NetworkBehaviourId so must be converted before
        ///     use)
        /// </summary>
        [Networked]
        [Capacity(MaxParticipants)]
        public NetworkDictionary<Player, CombatParticipant> Participators { get; }

        public override void FixedUpdateNetwork()
        {
            // Check if any hordes need to retreat, and make them leave combat.
            // If a player has no participating hordes, make them leave combat.

            List<HordeController> hordesToRemove = new();
            List<Player> playersToRemove = new();

            foreach (var kvp in Participators)
            {
                var aliveHordes = 0;
                foreach (var hordeID in kvp.Value.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    if (horde.TotalHealth > kvp.Value.HordeStartingHealth.Get(hordeID) * 0.2f)
                    {
                        aliveHordes++;
                    }
                    else
                    {
                        hordesToRemove.Add(horde);
                        // Tell horde to run away to nearest friendly POI
                        horde.RetreatRpc();
                    }
                }

                if (aliveHordes == 0) playersToRemove.Add(kvp.Key);
            }

            foreach (var horde in hordesToRemove)
            {
                var copy = Participators.Get(horde.Player);
                copy.RemoveHorde(horde);
                Participators.Set(horde.Player, copy);
            }

            foreach (var player in playersToRemove)
            {
                Participators.Remove(player);
                player.LeaveCombatRpc();
            }

            // If there's only one person left in combat they are the winner! Reset controller.
            if (Participators.Count == 1)
            {
                Participators.First().Key.LeaveCombatRpc();
                Participators.Clear();
                InitiatingPlayer = null;
            }
        }

        public void AddHorde(HordeController horde, bool voluntary)
        {
            if (Participators.Count == 0) InitiatingPlayer = horde.Player;

            if (!Participators.TryGet(horde.Player, out var participant))
            {
                if (!voluntary) horde.Player.EnterCombatRpc(this);

                Participators.Add(horde.Player, new CombatParticipant(horde.Player, horde, voluntary));
            }
            else
            {
                // Operates on local copy
                participant.AddHorde(horde, voluntary);
                // Update stored copy
                Participators.Set(horde.Player, participant);
            }
        }

        public HordeController GetNearestEnemy(HordeController me)
        {
            Vector2 myCenter = me.GetBounds().center;

            HordeController bestTarget = null;
            var closestDistance = Mathf.Infinity;

            foreach (var kvp in Participators)
            {
                // Only look at enemies
                if (kvp.Key == me.Player) continue;

                foreach (var hordeID in kvp.Value.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    var dist = ((Vector2)horde.GetBounds().center - myCenter).sqrMagnitude;

                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        bestTarget = horde;
                    }
                }
            }

            return bestTarget;
        }

        public bool HordeIsVoluntary(HordeController horde)
        {
            return Participators.Get(horde.Player).Voluntary.Get(horde);
        }

        public bool HordeInCombat(HordeController horde)
        {
            if (Participators.TryGet(horde.Player, out var participant))
                if (participant.Hordes.Contains(horde))
                    return true;

            return false;
        }
    }
}