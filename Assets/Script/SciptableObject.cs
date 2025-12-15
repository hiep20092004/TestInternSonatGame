using UnityEngine;
using System.Collections.Generic;

public enum LiquidType
{
    None,
    Red,
    Blue,
    Green,
    Yellow,
    Purple,
    Orange
}

[CreateAssetMenu(fileName = "LiquidTheme", menuName = "Game/Liquid Theme")]
public class LiquidThemeSO : ScriptableObject
{
    [System.Serializable]
    public struct LiquidDefinition
    {
        public LiquidType type;
        public Color color;
    }

    public List<LiquidDefinition> definitions;

    public Color GetColor(LiquidType type)
    {
        foreach (var def in definitions)
        {
            if (def.type == type) return def.color;
        }
        return Color.clear;
    }
}