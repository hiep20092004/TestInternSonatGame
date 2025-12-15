using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    private void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    [Header("Game Progression")]
    public int CurrentLevelIndex { get; private set; } = 1;

    [System.Serializable]
    public struct DifficultyProfile
    {
        public string Name;
        public int TotalBottles;
        public int EmptyBottles;
        public int BaseShuffleSteps;
    }

    [Header("Difficulty Settings")]
    [SerializeField] private DifficultyProfile easy = new DifficultyProfile { Name = "Easy", TotalBottles = 5, EmptyBottles = 2, BaseShuffleSteps = 10 };
    [SerializeField] private DifficultyProfile medium = new DifficultyProfile { Name = "Medium", TotalBottles = 7, EmptyBottles = 2, BaseShuffleSteps = 25 };
    [SerializeField] private DifficultyProfile hard = new DifficultyProfile { Name = "Hard", TotalBottles = 9, EmptyBottles = 2, BaseShuffleSteps = 50 };
    [SerializeField] private DifficultyProfile insane = new DifficultyProfile { Name = "Insane", TotalBottles = 12, EmptyBottles = 1, BaseShuffleSteps = 80 };

    [Header("Visuals & Audio")]
    [SerializeField] private BottleController bottlePrefab;
    [SerializeField] private Transform bottleContainer;
    [SerializeField] private LiquidThemeSO theme;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completeClip;
    [SerializeField] private AudioClip winClip;            
    [SerializeField] private AudioClip loseClip;

    [Header("Grid Layout")]
    [SerializeField] private float gapX = 2.0f;
    [SerializeField] private float gapY = 4.0f;
    [SerializeField] private int maxColumns = 4;

    private const int LAYERS_PER_BOTTLE = 4;
    public List<BottleController> AllBottles { get; private set; } = new List<BottleController>();

    private void Start()
    {
        CurrentLevelIndex = PlayerPrefs.GetInt("CurrentLevel", 1);
        GenerateLevel();
    }


    public void NextLevel()
    {
        CurrentLevelIndex++;
        PlayerPrefs.SetInt("CurrentLevel", CurrentLevelIndex);
        PlayerPrefs.Save();
        GenerateLevel();
    }

    [ContextMenu("Re-Generate Level")]
    public void GenerateLevel()
    {
        DifficultyProfile profile = GetProfile(CurrentLevelIndex);
     
        int steps = profile.BaseShuffleSteps + (CurrentLevelIndex * 2);
      
        var mapData = GenerateSolvableMapData(profile, steps);

        ClearOldBottles();
        SpawnNewBottles(mapData, profile.TotalBottles);

        UpdateUI();

        Debug.Log($"Level {CurrentLevelIndex} generated with {steps} shuffle steps.");
    }

    //Hàm sinh Map
    private List<List<LiquidType>> GenerateSolvableMapData(DifficultyProfile profile, int steps)
    {
        int filledCount = profile.TotalBottles - profile.EmptyBottles;
        var colors = GetValidColors();
        var map = new List<List<LiquidType>>();

        for (int i = 0; i < filledCount; i++)
        {
            var bottle = new List<LiquidType>();
            var color = colors[i % colors.Count];
            for (int k = 0; k < LAYERS_PER_BOTTLE; k++) bottle.Add(color);
            map.Add(bottle);
        }
        for (int i = 0; i < profile.EmptyBottles; i++) map.Add(new List<LiquidType>());
 
        int currentStep = 0;
        int maxLoop = steps * 10;
        int loop = 0;
        int lastSource = -1;

        while (currentStep < steps && loop < maxLoop)
        {
            loop++;
            int source = Random.Range(0, map.Count);
            int target = Random.Range(0, map.Count);


            if (source == target) continue;
            if (map[source].Count == 0) continue;
            if (map[target].Count >= LAYERS_PER_BOTTLE) continue; 
            if (target == lastSource) continue;

            var color = map[source][map[source].Count - 1];
            map[source].RemoveAt(map[source].Count - 1);
            map[target].Add(color);

            lastSource = source;
            currentStep++;
        }

        BreakPerfectBottles(map);

        return map;
    }

    private void BreakPerfectBottles(List<List<LiquidType>> map)
    {
        
        foreach (var bottle in map)
        {
            if (bottle.Count == LAYERS_PER_BOTTLE && bottle.All(c => c == bottle[0]))
            {
                var target = map.FirstOrDefault(b => b != bottle && b.Count < LAYERS_PER_BOTTLE);
                if (target != null)
                {
                    var color = bottle[bottle.Count - 1];
                    bottle.RemoveAt(bottle.Count - 1);
                    target.Add(color);
                }
            }
        }
    }

    //Hàm Spawn chai
    private void SpawnNewBottles(List<List<LiquidType>> mapData, int count)
    {
        int rows = Mathf.CeilToInt((float)count / maxColumns);
        float width = (Mathf.Min(count, maxColumns) - 1) * gapX;
        float height = (rows - 1) * gapY;
        Vector3 startPos = new Vector3(-width / 2, height / 2, 0);

        for (int i = 0; i < count; i++)
        {
            var bottle = Instantiate(bottlePrefab, bottleContainer);
            bottle.name = $"Bottle_{i}";

            int r = i / maxColumns;
            int c = i % maxColumns;
            bottle.transform.localPosition = startPos + new Vector3(c * gapX, -r * gapY, 0);

            var liquids = (i < mapData.Count) ? mapData[i].ToArray() : new LiquidType[0];
            bottle.Init(liquids, theme);
            AllBottles.Add(bottle);
        }
    }
    //Hàm dọn dẹp level cũ
    private void ClearOldBottles()
    {
        AllBottles.Clear();
        foreach (Transform child in bottleContainer)
        {
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    private DifficultyProfile GetProfile(int level)
    {
        if (level <= 5) return easy;
        if (level <= 15) return medium;
        if (level <= 30) return hard;
        return insane;
    }

    private List<LiquidType> GetValidColors()
    {
        return System.Enum.GetValues(typeof(LiquidType))
            .Cast<LiquidType>()
            .Where(t => t != LiquidType.None)
            .ToList();
    }

    private void UpdateUI()
    {
        try { if (UIManager.Instance) UIManager.Instance.UpdateLevelText(CurrentLevelIndex); } catch { }
    }

    public void PlayBottleCompleteSound()
    {
        if (audioSource && completeClip) audioSource.PlayOneShot(completeClip);
    }
    public void PlayWinSound()
    {
        if (audioSource && winClip) audioSource.PlayOneShot(winClip);
    }
    public void PlayLoseSound()
    {
        if (audioSource && loseClip) audioSource.PlayOneShot(loseClip);
    }
}