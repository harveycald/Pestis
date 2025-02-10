﻿using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    public class MapBehaviour : MonoBehaviour
    {
        public MapScriptableObject mapObject;
        public Tilemap tilemap;
        public LandBiomesList landBiomes;
        
        public void LoadRuntime()
        {
            tilemap.transform.position = new Vector2(0, -mapObject.height / 4.0f);

            mapObject.savedMap = new TextAsset(mapObject.mapBytes);
            if (mapObject.savedMap)
            {
                var biomeList = landBiomes.GetList();
                
                mapObject.width = BitConverter.ToInt32(mapObject.savedMap.bytes, 0);
                mapObject.height = BitConverter.ToInt32(mapObject.savedMap.bytes, 4);

                var mapBytes = mapObject.savedMap.GetData<byte>();
                
                int startIndex = 8;
                for (int x = 0; x < mapObject.width; ++x)
                {
                    for (int y = 0; y < mapObject.height; ++y)
                    {
                        int currentTileIndex = BitConverter.ToInt32(mapBytes.Slice(startIndex, 4).ToArray(), 0);
                        if (currentTileIndex == MapScriptableObject.WaterValue)
                        {
                            tilemap.SetTile(new Vector3Int(x, y), mapObject.water);
                        }
                        else
                        {
                            tilemap.SetTile(new Vector3Int(x, y), biomeList[currentTileIndex]);
                        }

                        startIndex += 4;
                    }
                }
                
                Debug.Log("Map loaded");
            }
            else
            {
                Debug.LogError("No map file found");
            }
        }
        
        public void Start()
        {
            LoadRuntime();
        }
    }
}