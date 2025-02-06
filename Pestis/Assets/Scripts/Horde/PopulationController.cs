// Population manager. Update birth and death rates here

using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using Random = System.Random;

namespace Horde
{
    public struct PopulationState : INetworkStruct
    {
        internal double BirthRate;
        internal double DeathRate;
        internal float HealthPerRat;

        /// <summary>
        ///     How much damage the horde does to other hordes per tick in combat
        /// </summary>
        internal float Damage;
    }

    public class PopulationController : NetworkBehaviour
    {
        public const int INITIAL_POPULATION = 5;

        // Soft population limit, should be extremely difficult for the player to grow beyond this, but still possible
        private const int PopMax = 1000;

        // The maximum change in a population per network tick
        private const int MaxPopGrowth = 1;
        public HordeController hordeController;

        private readonly Random _random = new();

        // Weights are reversed because weight decreases with distance from n
        // e.g. the probability of going from population n to n + 5 should be
        // smaller than going from n to n + 1, so the weight applied is smaller
        // for the former transition
        private readonly int[] _weights = Enumerable.Range(1, MaxPopGrowth).Reverse().ToArray();

        /// <summary>
        ///     Stores the highest health this horde has achieved. Used to stop the player losing too much progress.
        /// </summary>
        private float _highestHealth;

        private int _populationPeak = INITIAL_POPULATION;


        private List<double[]> _transitionMatrix;

        [Networked] private ref PopulationState State => ref MakeRef<PopulationState>();


        // Weight used in probability of population growth
        // W > 1 if R > P
        // W = 1 if R == P
        // W < 1 if R < P
        private double ResourceWeightGrowth()
        {
            return 1 + 0.5 * (1.0 - Math.Exp(-(hordeController.Player.CurrentCheese / hordeController.AliveRats - 1)));
        }

        // Weight used in probability of population decline
        // W = 1 if R >= P
        // W > 1 if R < P
        private double ResourceWeightDecline()
        {
            if (hordeController.Player.CurrentCheese >= hordeController.AliveRats) return 1.0;
            return Math.Exp(1.0 - hordeController.Player.CurrentCheese / hordeController.AliveRats);
        }

        // Calculate probability of population growth
        // Tapers off as population approaches PopMax
        private double Alpha(double weight, int population)
        {
            return State.BirthRate * weight * ((double)PopMax / (population + PopMax));
        }

        // Calculate probability of population decline
        private double Beta(double weight, int population)
        {
            return State.DeathRate * weight * ((double)PopMax / (population + PopMax));
        }

        // Normalize rows of a matrix to be between 0 and 1.0
        private void NormalizeRows(List<double[]> matrix)
        {
            for (var i = 0; i < _populationPeak; i++)
            {
                var ratio = 1.0 / matrix[i].Sum();
                matrix[i] = matrix[i].Select(o => o * ratio).ToArray();
            }
        }

        // Generate an InitialPopulation x (MaxPopGrowth * 2) + 1 transition matrix
        // Uses a list type so that rows can be added when a new populationPeak is reached
        // The entry X[n][n] is the probability of the population staying the same
        // The entry X[n][n+k] is the probability of the population growing by an amount k
        // The entry X[n][n-k] is the probability of the population declining by an amount k
        private List<double[]> GenerateTransitionMatrix()
        {
            var transitionMatrix = new List<double[]>(_populationPeak);
            var wMin = _weights.Min();
            var wMax = _weights.Max();
            for (var i = 0; i < _populationPeak; i++)
            {
                // Delta is the probability of staying at the same population
                double delta = 0;
                var row = new double[MaxPopGrowth * 2 + 1];
                for (var j = 0; j < MaxPopGrowth; j++)
                {
                    // Normalise w using MinMax rescaling
                    var w = wMin == wMax ? 1 : (double)(_weights[j] - wMin) / (wMax - wMin);

                    var alpha = Alpha(w, i + 1);
                    row[MaxPopGrowth + j + 1] = alpha;
                    delta += alpha;

                    var beta = Beta(w, i + 1);
                    row[MaxPopGrowth - j - 1] = beta;
                    delta += beta;
                }

                row[MaxPopGrowth] = Math.Max(1 - delta, 0);
                transitionMatrix.Add(row);
            }

            NormalizeRows(transitionMatrix);
            return transitionMatrix;
        }

        // Update the transition matrix when a new population peak is achieved
        // Logic is mostly similar to GenerateTransitionMatrix, except that you are
        // operating on a single row here
        private void UpdateTransitionMatrix()
        {
            var row = new double[MaxPopGrowth * 2 + 1];
            double delta = 0;
            var wMin = _weights.Min();
            var wMax = _weights.Max();
            for (var i = 0; i < MaxPopGrowth; i++)
            {
                var w = wMin == wMax ? 1 : (double)(_weights[i] - wMin) / (wMax - wMin);

                var alpha = Alpha(w, _populationPeak);
                row[MaxPopGrowth + i + 1] = alpha;
                delta += alpha;

                var beta = Beta(w, _populationPeak);
                row[MaxPopGrowth - i - 1] = beta;
                delta += beta;
            }

            row[MaxPopGrowth] = Math.Max(1 - delta, 0);
            // Normalize row
            var ratio = 1.0 / row.Sum();
            row = row.Select(o => o * ratio).ToArray();
            _transitionMatrix.Add(row);
        }

        public override void Spawned()
        {
            State.BirthRate = 0.01;
            State.DeathRate = 0.005;
            State.HealthPerRat = 5.0f;
            State.Damage = 0.5f;

            hordeController.TotalHealth = INITIAL_POPULATION * State.HealthPerRat;

            _transitionMatrix = GenerateTransitionMatrix();
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            if (hordeController.AliveRats > _populationPeak)
            {
                _populationPeak = hordeController.AliveRats;
                UpdateTransitionMatrix();
            }

            // Lookup the relevant probabilities for the current population
            var probabilities = _transitionMatrix[hordeController.AliveRats - 1];
            var growthWeight = ResourceWeightGrowth();
            var declineWeight = ResourceWeightDecline();
            for (var i = 0; i < MaxPopGrowth * 2 + 1; i++)
            {
                if (MaxPopGrowth < i) probabilities[i] *= growthWeight;

                if (MaxPopGrowth > i) probabilities[i] *= declineWeight;
            }

            var ratio = 1.0 / probabilities.Sum();
            probabilities = probabilities.Select(o => o * ratio).ToArray();
            // Use a CDF for doing a weighted sample of the transition states
            double cumulative = 0.0f;
            var cdf = new List<double>(MaxPopGrowth * 2 + 1);
            for (var i = 0; i < MaxPopGrowth * 2 + 1; i++)
            {
                cumulative += probabilities[i];
                cdf.Add(cumulative);
            }

            // Randomly pick a number and do a binary search for it in CDF, returns the closest match
            var r = _random.NextDouble() * cumulative;
            var nextState = cdf.BinarySearch(r);
            if (nextState < 0) nextState = ~nextState;
            hordeController.TotalHealth = (nextState - MaxPopGrowth + hordeController.AliveRats) * State.HealthPerRat;
        }

        // Only executed on State Authority
        public override void FixedUpdateNetwork()
        {
            // Suspend population simulation during combat to avoid interference
            if (!hordeController.InCombat) PopulationEvent();
            _highestHealth = Mathf.Max(hordeController.TotalHealth, _highestHealth);
        }

        public PopulationState GetState()
        {
            return State;
        }

        public void SetState(PopulationState newState)
        {
            State = newState;
        }
    }
}