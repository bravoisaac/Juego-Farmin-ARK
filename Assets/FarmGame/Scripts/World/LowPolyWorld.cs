using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CozyFarm
{
    public sealed class WorldReferences
    {
        public Transform[] plotAnchors;
        public Vector3 playerSpawn;
        public Material sharedMaterial;
        public Transform decorationRoot;
    }

    /// <summary>
    /// Deterministic, texture-free scenery. Decorations are baked into five meshes;
    /// only the ground, world bounds, barn and pond need physics components.
    /// This generator works both at runtime and from the scene-authoring editor tool.
    /// </summary>
    public static class LowPolyWorld
    {
        private static readonly Color Grass = Hex(0x83A56A);
        private static readonly Color PaleGrass = Hex(0xA7BA7C);
        private static readonly Color Soil = Hex(0x987050);
        private static readonly Color Wood = Hex(0x886442);
        private static readonly Color Cream = Hex(0xEEE0B8);
        private static readonly Color Leaf = Hex(0x5C865A);
        private static readonly Color Gold = Hex(0xE7B657);
        private static readonly Color Orange = Hex(0xDA8545);

        public static WorldReferences Build(Transform parent)
        {
            var world = new GameObject("Low poly farm").transform;
            world.SetParent(parent, false);
            var decorations = new GameObject("Combined scenery").transform;
            decorations.SetParent(world, false);

            Shader shader = Shader.Find("CozyFarm/LowPoly");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Farm vertex palette", color = Color.white, enableInstancing = true };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);

            var terrain = new Geometry();
            var plants = new Geometry();
            var buildings = new Geometry();
            var details = new Geometry();
            var water = new Geometry();
            var random = new System.Random(4317);

            BuildGround(terrain, random);
            BuildBarn(buildings, new Vector3(-11.7f, 0f, -6.5f));
            BuildPond(terrain, water, details, new Vector3(12.1f, 0f, -4.4f));
            BuildFence(buildings);
            BuildGardenProps(buildings, plants, details);

            // Trees frame the work area without hiding the plots or the player spawn.
            Vector3[] trees =
            {
                new Vector3(-14.6f,0,1.0f), new Vector3(-14.0f,0,6.7f),
                new Vector3(-13.8f,0,12.0f), new Vector3(-8.8f,0,12.8f),
                new Vector3(-3.3f,0,13.2f), new Vector3(3.5f,0,13.3f),
                new Vector3(8.6f,0,12.8f), new Vector3(14.4f,0,11.8f),
                new Vector3(14.0f,0,5.9f), new Vector3(14.7f,0,0.7f),
                new Vector3(-5.7f,0,-9.9f), new Vector3(6.1f,0,-10.3f)
            };
            for (int i = 0; i < trees.Length; i++)
                Tree(plants, trees[i], 0.82f + (float)random.NextDouble() * 0.30f, i % 3);

            // Background hills sit behind the fence, leaving the farm perfectly flat.
            for (int i = 0; i < 7; i++)
                terrain.Ellipsoid(new Vector3(-14f + i * 4.6f, -0.40f, 14.2f),
                    new Vector3(3.6f, 1.55f + (i % 3) * 0.35f, 2.2f),
                    i % 2 == 0 ? PaleGrass : Grass, 7, 3);

            for (int i = 0; i < 165; i++)
            {
                float x = Range(random, -15.8f, 15.8f);
                float z = Range(random, -10.6f, 14.2f);
                if (!IsDecorationSpace(x, z)) continue;
                var position = new Vector3(x, 0f, z);
                if (i % 9 == 0)
                    details.Ellipsoid(position + Vector3.up * 0.16f,
                        new Vector3(0.40f, 0.28f, 0.33f), Hex(0xAAA58C), 5, 3);
                else if (i % 3 == 0)
                    Flower(details, position, i % 2 == 0 ? Cream : Gold);
                else
                    GrassClump(plants, position, 0.22f + Range(random, 0f, 0.16f));
            }

            terrain.Create("Ground, paths and hills", decorations, material).isStatic = true;
            plants.Create("Trees and grass", decorations, material).isStatic = true;
            buildings.Create("Barn, fences and props", decorations, material).isStatic = true;
            details.Create("Rocks and flowers", decorations, material).isStatic = true;
            water.Create("Pond surface", decorations, material, false).isStatic = true;

            AddCollider(world, "Ground collision", new Vector3(0f, -0.55f, 1.5f), new Vector3(34f, 1f, 30f));
            AddCollider(world, "West boundary", new Vector3(-17.15f, 1.5f, 1.5f), new Vector3(0.4f, 4f, 30.5f));
            AddCollider(world, "East boundary", new Vector3(17.15f, 1.5f, 1.5f), new Vector3(0.4f, 4f, 30.5f));
            AddCollider(world, "South boundary", new Vector3(0f, 1.5f, -13.65f), new Vector3(34.5f, 4f, 0.4f));
            AddCollider(world, "North boundary", new Vector3(0f, 1.5f, 16.65f), new Vector3(34.5f, 4f, 0.4f));
            AddCollider(world, "Barn collision", new Vector3(-11.7f, 1.4f, -6.5f), new Vector3(6.5f, 2.9f, 4.8f));
            AddCollider(world, "Pond collision", new Vector3(12.1f, 0.3f, -4.4f), new Vector3(6.1f, 1f, 3.5f));

            var anchors = new Transform[18];
            var plotRoot = new GameObject("Plots").transform;
            plotRoot.SetParent(world, false);
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 6; column++)
                {
                    int index = row * 6 + column;
                    anchors[index] = new GameObject("Plot " + (index + 1).ToString("00")).transform;
                    anchors[index].SetParent(plotRoot, false);
                    anchors[index].localPosition = new Vector3(-7.5f + column * 3f, 0f, 1f + row * 3f);
                }

            return new WorldReferences
            {
                plotAnchors = anchors,
                playerSpawn = new Vector3(0f, 0f, -4f),
                sharedMaterial = material,
                decorationRoot = decorations
            };
        }

        /// <summary>One compact mesh per crop template: carrot, wheat, pumpkin.</summary>
        public static GameObject CreateCrop(Transform parent, int cropId, Material material)
        {
            var geometry = new Geometry();
            int kind = Mathf.Clamp(cropId, 0, 2);
            if (kind == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = new Vector3((i - 1) * 0.27f, 0.09f, i == 1 ? 0.16f : -0.12f);
                    geometry.Frustum(p, 0.06f, 0.12f, 0.24f, 5, Orange, Quaternion.identity);
                    for (int j = 0; j < 5; j++)
                    {
                        float angle = j * 72f * Mathf.Deg2Rad;
                        Vector3 tip = p + new Vector3(Mathf.Cos(angle) * 0.20f, 0.58f, Mathf.Sin(angle) * 0.20f);
                        LeafBlade(geometry, p + Vector3.up * 0.23f, tip, 0.065f, Leaf);
                    }
                }
            }
            else if (kind == 1)
            {
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 0.4f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * 0.24f, 0f, Mathf.Sin(angle) * 0.24f);
                    float h = 0.70f + (i % 3) * 0.08f;
                    geometry.Frustum(p, 0.025f, 0.016f, h, 4, Gold, Quaternion.identity);
                    geometry.Ellipsoid(p + Vector3.up * (h + 0.08f), new Vector3(0.10f, 0.23f, 0.09f), Gold, 5, 3);
                    LeafBlade(geometry, p + Vector3.up * 0.24f,
                        p + new Vector3(Mathf.Cos(angle) * 0.22f, 0.55f, Mathf.Sin(angle) * 0.22f), 0.06f, PaleGrass);
                }
            }
            else
            {
                geometry.Ellipsoid(new Vector3(-0.12f, 0.29f, 0.03f), new Vector3(0.43f, 0.32f, 0.40f), Orange, 10, 4);
                geometry.Ellipsoid(new Vector3(0.35f, 0.20f, -0.22f), new Vector3(0.27f, 0.23f, 0.26f), Gold, 8, 4);
                geometry.Frustum(new Vector3(-0.12f, 0.54f, 0.03f), 0.055f, 0.035f, 0.16f, 5, Wood, Quaternion.Euler(8, 0, -12));
                geometry.Frustum(new Vector3(0.35f, 0.40f, -0.22f), 0.04f, 0.025f, 0.13f, 5, Wood, Quaternion.identity);
                LeafBlade(geometry, new Vector3(-0.1f, 0.05f, 0.1f), new Vector3(-0.56f, 0.17f, 0.32f), 0.24f, Leaf);
                LeafBlade(geometry, new Vector3(0.1f, 0.05f, 0.1f), new Vector3(0.44f, 0.14f, 0.41f), 0.22f, Leaf);
            }
            return geometry.Create("Crop " + kind, parent, material);
        }

        public static GameObject CreateMarker(Transform parent, Material material)
        {
            var geometry = new Geometry();
            const float extent = 1.25f;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    geometry.Box(new Vector3(x * (extent - 0.22f), 0.08f, z * extent), new Vector3(0.55f, 0.06f, 0.09f), Cream);
                    geometry.Box(new Vector3(x * extent, 0.08f, z * (extent - 0.22f)), new Vector3(0.09f, 0.06f, 0.55f), Cream);
                }
            return geometry.Create("Selection marker", parent, material, false);
        }

        private static void BuildGround(Geometry geometry, System.Random random)
        {
            // Shared vertices are intentionally avoided to retain each triangle's flat normal.
            const int columns = 10;
            const int rows = 9;
            for (int x = 0; x < columns; x++)
                for (int z = 0; z < rows; z++)
                {
                    float x0 = -17f + x * 34f / columns;
                    float x1 = -17f + (x + 1) * 34f / columns;
                    float z0 = -13.5f + z * 30f / rows;
                    float z1 = -13.5f + (z + 1) * 30f / rows;
                    Color color = Color.Lerp(Grass, PaleGrass, Range(random, 0f, 0.25f));
                    geometry.Quad(new Vector3(x0, -0.05f, z0), new Vector3(x0, -0.05f, z1),
                        new Vector3(x1, -0.05f, z1), new Vector3(x1, -0.05f, z0), color);
                }
            geometry.Box(new Vector3(0f, -0.80f, 1.5f), new Vector3(34f, 1.48f, 30f), Soil);
            geometry.Box(new Vector3(0f, -1.63f, 1.5f), new Vector3(32.9f, 0.30f, 28.9f), Hex(0x766D57));

            Color path = Hex(0xD8C59A);
            geometry.Box(new Vector3(0.3f, -0.018f, -2.45f), new Vector3(20.2f, 0.045f, 1.65f), path);
            geometry.Box(new Vector3(-9.6f, -0.014f, -4.1f), new Vector3(1.6f, 0.045f, 3.1f), path);
            geometry.Box(new Vector3(0f, -0.012f, -6.7f), new Vector3(1.8f, 0.045f, 7.0f), path);
            for (int i = 0; i < 13; i++)
            {
                float x = -8.7f + i * 1.5f;
                geometry.Ellipsoid(new Vector3(x, -0.025f, -2.45f + (i % 2 == 0 ? 0.82f : -0.82f)),
                    new Vector3(0.65f, 0.06f, 0.25f), path, 5, 2);
            }
        }

        private static void BuildBarn(Geometry geometry, Vector3 p)
        {
            Color wall = Hex(0xC67B58);
            Color roof = Hex(0x4D6D63);
            geometry.Box(p + new Vector3(0f, 1.4f, 0f), new Vector3(6.5f, 2.8f, 4.8f), wall);
            geometry.Box(p + new Vector3(0f, 0.12f, 0f), new Vector3(6.7f, 0.25f, 5f), Hex(0xB3AA8B));
            geometry.Triangle(p + new Vector3(-3.25f, 2.8f, -2.4f), p + new Vector3(0f, 4.2f, -2.4f), p + new Vector3(3.25f, 2.8f, -2.4f), wall);
            geometry.Triangle(p + new Vector3(3.25f, 2.8f, 2.4f), p + new Vector3(0f, 4.2f, 2.4f), p + new Vector3(-3.25f, 2.8f, 2.4f), wall);
            geometry.Quad(p + new Vector3(-3.65f, 2.72f, -2.75f), p + new Vector3(-3.65f, 2.72f, 2.75f),
                p + new Vector3(0f, 4.27f, 2.75f), p + new Vector3(0f, 4.27f, -2.75f), roof);
            geometry.Quad(p + new Vector3(0f, 4.27f, -2.75f), p + new Vector3(0f, 4.27f, 2.75f),
                p + new Vector3(3.65f, 2.72f, 2.75f), p + new Vector3(3.65f, 2.72f, -2.75f), roof * 1.08f);
            geometry.Box(p + new Vector3(0f, 4.27f, 0f), new Vector3(0.13f, 0.13f, 5.65f), roof);
            for (int side = -1; side <= 1; side += 2)
            {
                geometry.Box(p + new Vector3(side * 3.16f, 1.4f, -2.45f), new Vector3(0.18f, 2.8f, 0.15f), Cream);
                geometry.Box(p + new Vector3(side * 0.59f, 1.13f, -2.48f), new Vector3(1.12f, 2.1f, 0.12f), Wood);
                geometry.Box(p + new Vector3(side * 2.22f, 1.72f, -2.48f), new Vector3(0.94f, 0.94f, 0.13f), Cream);
                geometry.Box(p + new Vector3(side * 2.22f, 1.72f, -2.56f), new Vector3(0.71f, 0.70f, 0.07f), Hex(0x69858A));
                geometry.Box(p + new Vector3(side * 2.22f, 1.72f, -2.61f), new Vector3(0.055f, 0.73f, 0.04f), Cream);
                geometry.Box(p + new Vector3(side * 2.22f, 1.72f, -2.61f), new Vector3(0.74f, 0.055f, 0.04f), Cream);
                geometry.Box(p + new Vector3(side * 0.59f, 1.12f, -2.57f), new Vector3(0.09f, 2.30f, 0.06f), Cream,
                    Quaternion.Euler(0f, 0f, side * 27f));
            }
            geometry.Box(p + new Vector3(0f, 2.28f, -2.51f), new Vector3(2.52f, 0.15f, 0.17f), Cream);
            geometry.Box(p + new Vector3(0f, 2.80f, -2.46f), new Vector3(6.55f, 0.14f, 0.12f), Cream);
            geometry.Ellipsoid(p + new Vector3(0f, 3.43f, -2.45f), new Vector3(0.34f, 0.34f, 0.06f), Cream, 8, 4);
            // Hay bales beside the barn, made from the same combined geometry.
            geometry.Box(p + new Vector3(3.9f, 0.43f, 0.1f), new Vector3(1.0f, 0.85f, 1.4f), Gold);
            geometry.Box(p + new Vector3(3.9f, 0.88f, 0.1f), new Vector3(0.13f, 0.06f, 1.43f), Wood);
        }

        private static void BuildPond(Geometry terrain, Geometry water, Geometry details, Vector3 p)
        {
            const int sides = 12;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1) * Mathf.PI * 2f / sides;
                terrain.Quad(p + new Vector3(Mathf.Cos(a) * 3.8f, 0.015f, Mathf.Sin(a) * 2.6f),
                    p + new Vector3(Mathf.Cos(a) * 3.35f, 0.10f, Mathf.Sin(a) * 2.15f),
                    p + new Vector3(Mathf.Cos(b) * 3.35f, 0.10f, Mathf.Sin(b) * 2.15f),
                    p + new Vector3(Mathf.Cos(b) * 3.8f, 0.015f, Mathf.Sin(b) * 2.6f), Hex(0xBEBC92));
                water.Triangle(p + new Vector3(0f, 0.075f, 0f),
                    p + new Vector3(Mathf.Cos(b) * 3.35f, 0.075f, Mathf.Sin(b) * 2.15f),
                    p + new Vector3(Mathf.Cos(a) * 3.35f, 0.075f, Mathf.Sin(a) * 2.15f),
                    i % 3 == 0 ? Hex(0x75B7AE) : Hex(0x66A99F));
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 lily = p + new Vector3(-1.5f + i * 0.68f, 0.09f, 0.5f + (i % 2) * 0.45f);
                water.Frustum(lily, 0.23f, 0.23f, 0.025f, 6, Leaf, Quaternion.identity);
                if (i % 2 == 0) details.Ellipsoid(lily + Vector3.up * 0.08f, new Vector3(0.08f, 0.07f, 0.08f), Cream, 5, 2);
            }
            for (int i = 0; i < 8; i++)
            {
                Vector3 stem = p + new Vector3(-2.8f + i * 0.12f, 0f, 1.3f + (i % 3) * 0.11f);
                details.Frustum(stem, 0.025f, 0.015f, 0.67f + (i % 2) * 0.2f, 4, Leaf, Quaternion.identity);
                details.Frustum(stem + Vector3.up * (0.57f + (i % 2) * 0.2f), 0.06f, 0.05f, 0.22f, 5, Wood, Quaternion.identity);
            }
        }

        private static void BuildFence(Geometry geometry)
        {
            for (int i = 0; i <= 10; i++)
                FencePost(geometry, new Vector3(-10.5f + i * 2.1f, 0f, 10f));
            for (int i = 0; i < 10; i++)
                FenceRails(geometry, new Vector3(-9.45f + i * 2.1f, 0f, 10f), 2.1f, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++) FencePost(geometry, new Vector3(side * 10.5f, 0f, 1.6f + i * 2.1f));
                for (int i = 0; i < 4; i++) FenceRails(geometry, new Vector3(side * 10.5f, 0f, 2.65f + i * 2.1f), 2.1f, true);
            }
        }

        private static void FencePost(Geometry geometry, Vector3 p)
        {
            geometry.Box(p + Vector3.up * 0.52f, new Vector3(0.16f, 1.04f, 0.16f), Cream);
            geometry.Frustum(p + Vector3.up * 1.04f, 0.13f, 0f, 0.16f, 4, Cream, Quaternion.Euler(0f, 45f, 0f));
        }

        private static void FenceRails(Geometry geometry, Vector3 p, float length, bool alongZ)
        {
            Vector3 size = alongZ ? new Vector3(0.10f, 0.12f, length) : new Vector3(length, 0.12f, 0.10f);
            geometry.Box(p + Vector3.up * 0.43f, size, Cream);
            geometry.Box(p + Vector3.up * 0.80f, size, Cream);
        }

        private static void BuildGardenProps(Geometry buildings, Geometry plants, Geometry details)
        {
            // Entrance sign, crates, well and a little cluster of pumpkins.
            buildings.Box(new Vector3(2.1f, 0.58f, -7.8f), new Vector3(0.15f, 1.16f, 0.15f), Wood);
            buildings.Box(new Vector3(2.1f, 1.17f, -7.8f), new Vector3(1.8f, 0.66f, 0.16f), Cream, Quaternion.Euler(0f, 0f, -3f));
            details.Ellipsoid(new Vector3(2.1f, 1.19f, -7.91f), new Vector3(0.19f, 0.20f, 0.04f), Orange, 6, 3);
            LeafBlade(details, new Vector3(2.1f, 1.3f, -7.92f), new Vector3(2.3f, 1.39f, -7.92f), 0.09f, Leaf);
            buildings.Box(new Vector3(-3f, 0.32f, -6.5f), new Vector3(1.1f, 0.65f, 0.8f), Wood);
            for (int i = 0; i < 3; i++)
                buildings.Box(new Vector3(-3f, 0.13f + i * 0.22f, -6.92f), new Vector3(1.15f, 0.12f, 0.05f), Hex(0xB78E58));
            for (int i = 0; i < 3; i++)
                details.Ellipsoid(new Vector3(-3.3f + i * 0.3f, 0.72f, -6.5f), new Vector3(0.18f, 0.17f, 0.19f), Orange, 7, 3);

            Vector3 well = new Vector3(6.3f, 0f, -6.6f);
            buildings.Frustum(well, 0.70f, 0.70f, 0.64f, 10, Hex(0xB5AC91), Quaternion.identity);
            buildings.Frustum(well + Vector3.up * 0.64f, 0.59f, 0.59f, 0.015f, 10, Hex(0x456D68), Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
                buildings.Box(well + new Vector3(side * 0.66f, 1.05f, 0f), new Vector3(0.13f, 2.1f, 0.13f), Wood);
            buildings.Box(well + new Vector3(0f, 1.65f, 0f), new Vector3(1.6f, 0.12f, 0.12f), Wood);
            buildings.Box(well + new Vector3(0f, 2.15f, 0f), new Vector3(2.05f, 0.16f, 1.5f), Hex(0x4D6D63));
            buildings.Box(well + new Vector3(0f, 1.35f, 0f), new Vector3(0.028f, 0.65f, 0.028f), Cream);
            plants.Ellipsoid(new Vector3(8.2f, 0.35f, -8.2f), new Vector3(0.90f, 0.55f, 0.66f), Leaf, 7, 3);
            plants.Ellipsoid(new Vector3(8.9f, 0.25f, -7.9f), new Vector3(0.55f, 0.42f, 0.50f), PaleGrass, 7, 3);
        }

        private static void Tree(Geometry geometry, Vector3 p, float scale, int type)
        {
            geometry.Frustum(p, 0.19f * scale, 0.10f * scale, 2.1f * scale, 6, Wood, Quaternion.identity);
            if (type == 2)
            {
                geometry.Frustum(p + Vector3.up * (1.3f * scale), 1.25f * scale, 0f, 2.7f * scale, 7, Leaf, Quaternion.identity);
                geometry.Frustum(p + Vector3.up * (2.3f * scale), 0.92f * scale, 0f, 2.2f * scale, 7, PaleGrass, Quaternion.identity);
            }
            else
            {
                Color color = type == 0 ? Leaf : Hex(0x92A85D);
                geometry.Ellipsoid(p + new Vector3(0f, 2.8f * scale, 0f), new Vector3(1.42f, 1.75f, 1.37f) * scale, color, 7, 4);
                geometry.Ellipsoid(p + new Vector3(-0.85f, 2.0f, 0.2f) * scale, new Vector3(0.94f, 0.96f, 0.94f) * scale, color, 6, 3);
                geometry.Ellipsoid(p + new Vector3(0.8f, 2.3f, -0.1f) * scale, new Vector3(0.9f, 1.1f, 0.9f) * scale, color, 6, 3);
            }
        }

        private static void GrassClump(Geometry geometry, Vector3 p, float height)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                LeafBlade(geometry, p, p + new Vector3(Mathf.Cos(a) * 0.16f, height, Mathf.Sin(a) * 0.16f), 0.045f, PaleGrass);
            }
        }

        private static void Flower(Geometry geometry, Vector3 p, Color color)
        {
            geometry.Frustum(p, 0.018f, 0.01f, 0.27f, 3, Leaf, Quaternion.identity);
            geometry.Ellipsoid(p + Vector3.up * 0.27f, new Vector3(0.10f, 0.07f, 0.10f), color, 5, 2);
        }

        private static void LeafBlade(Geometry geometry, Vector3 root, Vector3 tip, float width, Color color)
        {
            Vector3 direction = tip - root;
            Vector3 side = Vector3.Cross(direction, Vector3.forward).normalized * width;
            if (side.sqrMagnitude < 0.00001f) side = Vector3.right * width;
            Vector3 center = Vector3.Lerp(root, tip, 0.45f) + Vector3.up * width * 0.25f;
            // Thin closed diamond: visible from above and from either side.
            Vector3 a = center - side;
            Vector3 b = center + side;
            geometry.Triangle(root, a, tip, color);
            geometry.Triangle(root, tip, b, color);
            geometry.Triangle(root, tip, a, color * 0.90f);
            geometry.Triangle(root, b, tip, color * 0.90f);
        }

        private static bool IsDecorationSpace(float x, float z)
        {
            if (x > -11f && x < 11f && z > -3.7f && z < 11f) return false;
            if (x > -16f && x < -7.5f && z > -10f && z < -3f) return false;
            if (x > 8f && z > -7.5f && z < -1f) return false;
            if (Mathf.Abs(x) < 2.4f && z < -3f) return false;
            return true;
        }

        private static void AddCollider(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.isStatic = true;
            go.AddComponent<BoxCollider>().size = size;
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static Color Hex(uint value)
        {
            return new Color(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f, 1f);
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>(4096);
            private readonly List<Vector3> normals = new List<Vector3>(4096);
            private readonly List<Color> colors = new List<Color>(4096);
            private readonly List<int> triangles = new List<int>(4096);

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 0.00000001f) return;
                normal.Normalize();
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                colors.Add(color); colors.Add(color); colors.Add(color);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                Triangle(a, b, c, color);
                Triangle(a, c, d, color);
            }

            public void Box(Vector3 center, Vector3 size, Color color)
            {
                Box(center, size, color, Quaternion.identity);
            }

            public void Box(Vector3 center, Vector3 size, Color color, Quaternion rotation)
            {
                Vector3 h = size * 0.5f;
                Vector3 a = center + rotation * new Vector3(-h.x, -h.y, -h.z);
                Vector3 b = center + rotation * new Vector3(-h.x, -h.y, h.z);
                Vector3 c = center + rotation * new Vector3(-h.x, h.y, -h.z);
                Vector3 d = center + rotation * new Vector3(-h.x, h.y, h.z);
                Vector3 e = center + rotation * new Vector3(h.x, -h.y, -h.z);
                Vector3 f = center + rotation * new Vector3(h.x, -h.y, h.z);
                Vector3 g = center + rotation * new Vector3(h.x, h.y, -h.z);
                Vector3 h2 = center + rotation * new Vector3(h.x, h.y, h.z);
                Quad(a, c, g, e, color);
                Quad(f, h2, d, b, color);
                Quad(b, d, c, a, color);
                Quad(e, g, h2, f, color);
                Quad(c, d, h2, g, color);
                Quad(a, e, f, b, color * 0.92f);
            }

            public void Frustum(Vector3 center, float bottomRadius, float topRadius, float height,
                int sides, Color color, Quaternion rotation)
            {
                Vector3 top = center + rotation * (Vector3.up * height);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float b = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 directionA = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 directionB = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                    Vector3 ba = center + rotation * (directionA * bottomRadius);
                    Vector3 bb = center + rotation * (directionB * bottomRadius);
                    Vector3 ta = top + rotation * (directionA * topRadius);
                    Vector3 tb = top + rotation * (directionB * topRadius);
                    Quad(ba, ta, tb, bb, color);
                    Triangle(top, tb, ta, color);
                    Triangle(center, ba, bb, color);
                }
            }

            public void Ellipsoid(Vector3 center, Vector3 radius, Color color, int sides, int rings)
            {
                for (int ring = 0; ring < rings; ring++)
                {
                    float low = -Mathf.PI * 0.5f + Mathf.PI * ring / rings;
                    float high = -Mathf.PI * 0.5f + Mathf.PI * (ring + 1) / rings;
                    for (int side = 0; side < sides; side++)
                    {
                        float a = side * Mathf.PI * 2f / sides;
                        float b = (side + 1) * Mathf.PI * 2f / sides;
                        Color face = color * (0.96f + ((side + ring) % 3) * 0.025f);
                        Quad(EllipsoidPoint(center, radius, low, a), EllipsoidPoint(center, radius, high, a),
                            EllipsoidPoint(center, radius, high, b), EllipsoidPoint(center, radius, low, b), face);
                    }
                }
            }

            private static Vector3 EllipsoidPoint(Vector3 center, Vector3 radius, float latitude, float longitude)
            {
                return center + new Vector3(Mathf.Cos(latitude) * Mathf.Cos(longitude) * radius.x,
                    Mathf.Sin(latitude) * radius.y, Mathf.Cos(latitude) * Mathf.Sin(longitude) * radius.z);
            }

            public GameObject Create(string name, Transform parent, Material material, bool castShadows = true)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                return go;
            }
        }
    }
}
