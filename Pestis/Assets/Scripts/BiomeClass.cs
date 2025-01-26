using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeClass : MonoBehaviour
{
    public BiomeTile[] TileList;
    public GameObject[] FeatureList; //set of features that spawn in this biome
    IsometricRuleTile seedTile;
    List<IsometricRuleTile> tiles;
    public virtual void FeatureGeneration()
    {
        // Empty method
    }

    public virtual void Growth()//generate biomes by seeding 1 biome and then repeatedly "growing" it
    {
        // Empty method
    }
}
