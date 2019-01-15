using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

public enum PlayerTeam
{
    None,
    BlueTeam,

    RedTeam
}

public class SpawnManager : MonoBehaviour
{
    [SerializeField] private List<SpawnPoint> _sharedSpawnPoints = new List<SpawnPoint>();
    System.Random _random = new System.Random();



    [Tooltip("This will be used to calculate the second filter where algorithm looks for closest friends, if the friends are away from this value, they will be ignored")]
    [SerializeField] private float _maxDistanceToClosestFriend = 30;
    [Tooltip("This will be used to calculate the first filter where algorithm looks for enemies that are far away from this value. Only enemies which are away from this value will be calculated.")]
    [SerializeField] private float _minDistanceToClosestEnemy = 10;
    [Tooltip("This value is to prevent friendly player spawning on top of eachothers. If a player is within the range of this value to a spawn point, that spawn point will be ignored")]
    [SerializeField] private float _minMemberDistance = 2;


    public DummyPlayer PlayerToBeSpawned;
    public DummyPlayer[] DummyPlayers;

    private void Awake()
    {
        _sharedSpawnPoints.AddRange(FindObjectsOfType<SpawnPoint>());
        DummyPlayers = FindObjectsOfType<DummyPlayer>();
    }

    #region SPAWN ALGORITHM
    public SpawnPoint GetSharedSpawnPoint(PlayerTeam team)
    {
        List<SpawnPoint> spawnPoints = new List<SpawnPoint>(_sharedSpawnPoints.Count);
        CalculateDistancesForSpawnPoints(team);
        GetSpawnPointsByDistanceSpawning(team, ref spawnPoints);
        if (spawnPoints.Count == 0)
        {
            GetSpawnPointsBySquadSpawning(team, ref spawnPoints);
        }
        SpawnPoint spawnPoint = spawnPoints.Count <= 1 ? spawnPoints[0] : spawnPoints[_random.Next(0, (int)((float)spawnPoints.Count * .5f))];
        spawnPoint.StartTimer();
        return spawnPoint;
    }

    private void GetSpawnPointsByDistanceSpawning(PlayerTeam team, ref List<SpawnPoint> suitableSpawnPoints)
    {
        
        
        if (suitableSpawnPoints == null)
        {
            suitableSpawnPoints = new List<SpawnPoint>();
        }
        suitableSpawnPoints.Clear();
        ReverseSortSpawnPointsByEnemyDistance();  // Büyükten küçüğe doğru spawn noktalarını sıralar. 

        for (int i = 0; i < _sharedSpawnPoints.Count && _sharedSpawnPoints[i].DistanceToClosestEnemy > _minDistanceToClosestEnemy; i++) // Bir sonraki spawn noktasının en yakın düşman uzaklığı, belirlenen en yakın düşman uzaklağından büyük sürece döngü devam eder.
        {
            int _minPlayerDistance = Math.Min((int)_sharedSpawnPoints[i].DistanceToClosestEnemy, (int)_sharedSpawnPoints[i].DistanceToClosestFriend); // Spawn noktasının herhangi bir oyuncuya olan uzaklığını tutar.
            if (_minPlayerDistance > _minMemberDistance && _sharedSpawnPoints[i].SpawnTimer <= 0) // Minimum oyuncuya olan uzaklık belirlenen en yakın uzaklıktan büyükse ve iki saniyedir o nokta seçilmemiş ise spawn noktası aday listeye eklenir.
            {
                suitableSpawnPoints.Add(_sharedSpawnPoints[i]);
                print(i + ". nokta eklendi. \nEn yakın düşman uzaklığı: " + _sharedSpawnPoints[i].DistanceToClosestEnemy + " - En yakın oyuncu uzaklığı: " + _minPlayerDistance);

            }
        }
        if (suitableSpawnPoints.Count <= 0) // Hiçbir Nokta uyumlu değil ise ilk spawn noktasına atanır.
        {
            print("Uygun nokta bulunamadığı için ilk Spawn noktasına atandı.");
            suitableSpawnPoints.Add(_sharedSpawnPoints[0]);
        }

    }
    private void GetSpawnPointsBySquadSpawning(PlayerTeam team, ref List<SpawnPoint> suitableSpawnPoints)
    {
        if (suitableSpawnPoints == null)
        {
            suitableSpawnPoints = new List<SpawnPoint>();
        }
        suitableSpawnPoints.Clear();
        SortSpawnPointsByFriendDistance();

        for (int i = 0; i < _sharedSpawnPoints.Count && _sharedSpawnPoints[i].DistanceToClosestFriend > _maxDistanceToClosestFriend; i++)
        {
            if (_sharedSpawnPoints[i].DistanceToClosestFriend > _minMemberDistance && _sharedSpawnPoints[i].DistanceToClosestEnemy > _minMemberDistance && _sharedSpawnPoints[i].SpawnTimer <= 0)
            {
                suitableSpawnPoints.Add(_sharedSpawnPoints[i]);
            }
        }
        if (suitableSpawnPoints.Count <= 0)
        {
            print("Uygun nokta bulunamadığı için ilk Spawn noktasına atandı.");
            suitableSpawnPoints.Add(_sharedSpawnPoints[0]);
        }

    }

    private void CalculateDistancesForSpawnPoints(PlayerTeam playerTeam)
    {
        for (int i = 0; i < _sharedSpawnPoints.Count; i++)
        {
            _sharedSpawnPoints[i].DistanceToClosestFriend = GetDistanceToClosestMember(_sharedSpawnPoints[i].PointTransform.position, playerTeam);
            _sharedSpawnPoints[i].DistanceToClosestEnemy = GetDistanceToClosestMember(_sharedSpawnPoints[i].PointTransform.position, playerTeam == PlayerTeam.BlueTeam ? PlayerTeam.RedTeam : playerTeam == PlayerTeam.RedTeam ? PlayerTeam.BlueTeam : PlayerTeam.None);
        }
    }

    private void SortSpawnPointsByFriendDistance()
    {
        _sharedSpawnPoints.Sort(delegate (SpawnPoint a, SpawnPoint b)
        {
            if (a.DistanceToClosestFriend == b.DistanceToClosestFriend)
            {
                return 0;
            }
            if (a.DistanceToClosestFriend > b.DistanceToClosestFriend)
            {
                return 1;
            }
            return -1;
        });
    }

    private void ReverseSortSpawnPointsByEnemyDistance()
    {
        _sharedSpawnPoints.Sort(delegate (SpawnPoint a, SpawnPoint b)
        {
            if (a.DistanceToClosestEnemy == b.DistanceToClosestEnemy)
            {
                return 0;
            }
            if (a.DistanceToClosestEnemy < b.DistanceToClosestEnemy)
            {
                return 1;
            }
            return -1;
        });
    }

    private float GetDistanceToClosestMember(Vector3 position, PlayerTeam playerTeam)
    {
        //Minimum uzaklığı doğru hesaplayabilmek için ilk değer olabilecek en büyük değer olmalıdır
        float _closestDistance = float.MaxValue;
        foreach (var player in DummyPlayers)
        {

            if (!player.Disabled && player.PlayerTeamValue != PlayerTeam.None && player.PlayerTeamValue == playerTeam && !player.IsDead())
            {
                float playerDistanceToSpawnPoint = Vector3.Distance(position, player.Transform.position);

                if (playerDistanceToSpawnPoint < _closestDistance)
                {
                    _closestDistance = playerDistanceToSpawnPoint;
                }
            }
        }

        return _closestDistance;
    }

    #endregion
    /// <summary>
    /// Test için paylaşımlı spawn noktalarından en uygun olanını seçer.
    /// Test oyuncusunun pozisyonunu seçilen spawn noktasına atar.
    /// </summary>
    public void TestGetSpawnPoint()
    {
        Utils.ClearLogConsole();
        SpawnPoint spawnPoint = GetSharedSpawnPoint(PlayerToBeSpawned.PlayerTeamValue);
        PlayerToBeSpawned.Transform.position = spawnPoint.PointTransform.position;
        
    }
   
}

/**
 * Konsol çıktısını temizler.
 **/
public static class Utils
{
    static MethodInfo _clearConsoleMethod;
    static MethodInfo clearConsoleMethod
    {
        get
        {
            if (_clearConsoleMethod == null)
            {
                Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
                Type logEntries = assembly.GetType("UnityEditor.LogEntries");
                _clearConsoleMethod = logEntries.GetMethod("Clear");
            }
            return _clearConsoleMethod;
        }
    }

    public static void ClearLogConsole()
    {
        clearConsoleMethod.Invoke(new object(), null);
    }
}