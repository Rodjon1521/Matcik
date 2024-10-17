using UnityEngine;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
// ReSharper disable All
public class MyGame : MonoBehaviour
{
    public Entity potionPrefab;
    public Entity zombiePrefab;
    public Entity player;
    public float potionSpawnInterval;
    public float zombieSpawnInterval;
    public float potionSpawnT;
    public float zombieSpawnT;

    
    public float infectTimerT = 10f;
    public bool isInfected = false;

    public bool inputEnabled = true;
    public float inputDisableTimer = 3f;
    
    public KeyCode currentRandomKey;
    public float keyChangeInterval = 3f;
    public float keyChangeTimer = 0f;
    
    public float separationDistance = 10.0f;
    public float alignmentWeight = 2.0f;
    public float cohesionWeight = 1.5f;
    public float separationWeight = 2.0f;

    public List<Entity> entities = new(256);

    public void Start()
    {
        player.transform.position = new Vector3(0, 1, -2);
        entities.Add(player);
    }

    public void Update()
    {
        if (inputEnabled == true)
        {
            UpdateInput();
        }
        else
        {
            NewInfectedInput();
        }

        UpdatePotions();
        UpdateZombies();
        UpdateInfectionTimer();
    }

    public List<Entity> GetEntitiesOfType(EntityType type)
    {
        List<Entity> result = new(entities.Count);
        for (int i = 0; i < entities.Count; i++)
        {
            if ((entities[i].type & type) != 0)
            {
                result.Add(entities[i]);
            }
        }

        return result;
    }

    public Entity FindNearestEntity(Entity e, List<Entity> others, Func<Entity, bool> SomeFunction = null)
    {
        Entity nearestEntity = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < others.Count; i++)
        {
            if (others[i] != e && (SomeFunction == null || SomeFunction(others[i])))
            {
                float distance = Vector3.Distance(e.transform.position, others[i].transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestEntity = others[i];
                }
            }
        }

        return nearestEntity;
    }
    public void UpdateZombies()
    {
        List<Entity> zombies = GetEntitiesOfType(EntityType.Zombie);
        List<Entity> potions = GetEntitiesOfType(EntityType.Potion);

        if (zombies.Count < 10)
        {
            zombieSpawnT -= Time.deltaTime;

            if (zombieSpawnT <= 0)
            {
                zombieSpawnT += zombieSpawnInterval;
                Entity zombie = SpawnEntity(zombiePrefab);
                zombies.Add(zombie);
            }
        }

    
        for (int i = 0; i < zombies.Count; i++)
        {
            // NOTE(sqd): Update every zombie           
            Entity zombie = zombies[i];
            
            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            int neighborCount = 0;
            for (int j = 0; j < zombies.Count; j++)
            {
                if (i != j)
                {
                    Entity neighbor = zombies[j];
                    float distance = Vector3.Distance(zombie.transform.position, neighbor.transform.position);

                    if (distance < separationDistance)
                    {
                        // Separation: отталкиваемся от других зомби
                        separation += (zombie.transform.position - neighbor.transform.position).normalized / distance;
                    }

                    // Alignment: вычисляем среднее направление
                    alignment += neighbor.transform.forward;

                    // Cohesion: двигаемся к средней позиции соседей
                    cohesion += neighbor.transform.position;

                    neighborCount++;
                }
            }

            if (neighborCount > 0)
            {
                // Средний вектор для выравнивания
                alignment /= neighborCount;
                alignment.Normalize();

                // Средняя позиция для сцепления
                cohesion /= neighborCount;
                cohesion = (cohesion - zombie.transform.position).normalized;

                // Применяем итоговые векторы с весами
                Vector3 finalDirection = (separation * separationWeight) + (alignment * alignmentWeight) + (cohesion * cohesionWeight);
                finalDirection.Normalize();

                zombie.transform.LookAt(zombie.transform.position + finalDirection);
                zombie.transform.position += finalDirection * zombie.speed * Time.deltaTime;
            }

            // NOTE(sqd): Heal other zombies
            if (zombie.isHealed)
            {
                if (zombie.HasPotion())
                {
                    Entity nearestZombie = FindNearestEntity(zombie, zombies, (Entity e) => !e.isHealed);

                    if (nearestZombie != null)
                    {
                        zombie.transform.LookAt(nearestZombie.transform);
                        float moveDistance = zombie.speed * Time.deltaTime;
                        zombie.transform.position += zombie.transform.forward * moveDistance;

                        if (Vector3.Distance(zombie.transform.position, nearestZombie.transform.position) < 1.5f)
                        {
                            EntityHealEntity(zombie, nearestZombie);
                        }
                    }
                }
                else
                {
                    Entity nearestPotion = FindNearestEntity(zombie, potions);

                    if (nearestPotion != null)
                    {
                        zombie.transform.LookAt(nearestPotion.transform);
                        float moveDistance = zombie.speed * Time.deltaTime;
                        zombie.transform.position += zombie.transform.forward * moveDistance;

                        if (Vector3.Distance(zombie.transform.position, nearestPotion.transform.position) < 1.5f)
                        {
                            KillEntity(nearestPotion);
                            zombie.potionsCount++;
                        }
                    }
                }
            }
            else
            {
                List<Entity> entitiesToFilter = GetEntitiesOfType(EntityType.Player | EntityType.Zombie);
                Entity entityToFollow = FindNearestEntity(zombie, entitiesToFilter, (Entity e) => e.isHealed);
                if (entityToFollow != null)
                {
                    zombie.transform.LookAt(entityToFollow.transform);

                    float moveDistance = zombie.speed * Time.deltaTime;
                    zombie.transform.position += zombie.transform.forward * moveDistance;

                    // NOTE(sqd): Check if player near by
                    if (Vector3.Distance(entityToFollow.transform.position, zombie.transform.position) < 3)
                    {
                        if (entityToFollow.potionsCount > 0)
                        {
                            EntityHealEntity(entityToFollow, zombie);
                        }
                        else
                        {
                            if (entityToFollow.type == EntityType.Player)
                            {
                                isInfected = true;

                                // TODO(sqd): Make player death
                            }
                            else
                            {
                                EntityInfectEntity(zombie, entityToFollow);
                            }
                        }
                    }
                }
            }
        }
    }

    public void EntityInfectEntity(Entity e, Entity entityToInfect)
    {
        entityToInfect.isHealed = false;
        entityToInfect.speed /= 2;
        entityToInfect.mr.sharedMaterial = entityToInfect.notHealedMat;
    }

    public void EntityHealEntity(Entity e, Entity entityToHeal)
    {
        e.potionsCount--;
        entityToHeal.isHealed = true;
        entityToHeal.speed *= 2;
        entityToHeal.mr.sharedMaterial = entityToHeal.healedMat;
    }

    public void UpdatePotions()
    {
        List<Entity> potions = GetEntitiesOfType(EntityType.Potion);
        // NOTE(sqd): Spawn potions
        if (potions.Count < 10)
        {
            potionSpawnT -= Time.deltaTime;

            if (potionSpawnT <= 0)
            {
                potionSpawnT += potionSpawnInterval;
                Entity potion = SpawnEntity(potionPrefab);
            }
        }

        // NOTE(sqd): Rotate potions over time
        for (int i = 0; i < potions.Count; i++)
        {
            potions[i].transform.Rotate(0, potions[i].rotationSpeed * Time.deltaTime, 0);

            // NOTE(sqd): Check if player near by
            if (Vector3.Distance(player.transform.position, potions[i].transform.position) < 5)
            {
                KillEntity(potions[i]);
                // TODO(sqd): Should we remove potion from potions list?
                player.potionsCount++;
            }
        }
    }

    public Entity SpawnEntity(Entity prefab)
    {
        float randomX = Random.Range(-100, 100);
        float randomZ = Random.Range(-100, 100);
        Vector3 randomPosition = new Vector3(randomX, 0, randomZ);

        Entity result = Instantiate(prefab, randomPosition, Quaternion.identity);
        entities.Add(result);

        return result;
    }

    public void KillEntity(Entity e)
    {
        GameObject.Destroy(e.gameObject);
        entities.Remove(e);
    }

    public void UpdateInput()
    {
        player.speed = 50f;
        player.rotationSpeed = 200f;
        float rotationY = player.rotationSpeed * Time.deltaTime;
        float moveDistance = player.speed * Time.deltaTime;

        if (Input.GetKey(KeyCode.W))
        {
            player.transform.position += player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.A))
        {
            player.transform.position -= player.transform.right * moveDistance;
        }

        if (Input.GetKey(KeyCode.S))
        {
            player.transform.position -= player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.D))
        {
            player.transform.position += player.transform.right * moveDistance;
        }

        if (Input.GetKey(KeyCode.E))
        {
            player.transform.Rotate(0, rotationY, 0);
        }

        if (Input.GetKey(KeyCode.Q))
        {
            player.transform.Rotate(0, -rotationY, 0);
        }
    }

    public void UpdateInfectionTimer()
    {
        if (isInfected)
        {

            if (infectTimerT > 0)
            {
                infectTimerT -= Time.deltaTime;
                inputDisableTimer -= Time.deltaTime;

                if (inputDisableTimer <= 0)
                {
                    inputEnabled = !inputEnabled;
                    inputDisableTimer = 3f;
                }

            }
            else if (!hasFallen)
            {
                EnableFall();

                // ragdoll death

            }
        }
    }
    public bool hasFallen = false;
    void EnableFall()
    {
        player.GetComponent<Rigidbody>().isKinematic = false;
        player.GetComponent<Rigidbody>().useGravity = true;
        player.GetComponent<Rigidbody>().AddForce(Vector3.back * 5f, ForceMode.Impulse);
        hasFallen = true;
    }
    
    

    public static KeyCode[] inputKeys = new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.E, KeyCode.Q };
    public static KeyCode[] infectedKeys;

    public static KeyCode[] ShuffleArray(KeyCode[] array)
    {
        KeyCode[] newArray = (KeyCode[])array.Clone();
        int n = newArray.Length;

        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            KeyCode temp = newArray[i];
            newArray[i] = newArray[j];
            newArray[j] = temp;
        }

        return newArray;
    }

    public void InfectedInput()
    {
        float rotationY = player.rotationSpeed * Time.deltaTime;
        float moveDistance = player.speed * Time.deltaTime;
        
        keyChangeTimer -= Time.deltaTime;

        
        if (keyChangeTimer <= 0)
        {
            keyChangeTimer = keyChangeInterval;
            infectedKeys = ShuffleArray(inputKeys);
        }

        if (Input.GetKey(infectedKeys[0]))
        {
            player.transform.position += player.transform.forward * moveDistance;
        }

        if (Input.GetKey(infectedKeys[1]))
        {
            player.transform.position -= player.transform.right * moveDistance;
        }

        if (Input.GetKey(infectedKeys[2]))
        {
            player.transform.position -= player.transform.forward * moveDistance;
        }

        if (Input.GetKey(infectedKeys[3]))
        {
            player.transform.position += player.transform.right * moveDistance;
        }

        if (Input.GetKey(infectedKeys[4]))
        {
            player.transform.Rotate(0, rotationY, 0);
        }

        if (Input.GetKey(infectedKeys[5]))
        {
            player.transform.Rotate(0, -rotationY, 0);
        }
    }
    public void NewInfectedInput()
    {
        player.speed = 15f;
        player.rotationSpeed = 20f;
        float rotationY = player.rotationSpeed * Time.deltaTime;
        float moveDistance = player.speed * Time.deltaTime;

        if (Input.GetKey(KeyCode.W))
        {
            player.transform.position += player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.A))
        {
            player.transform.position -= player.transform.right * moveDistance;
        }

        if (Input.GetKey(KeyCode.S))
        {
            player.transform.position -= player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.D))
        {
            player.transform.position += player.transform.right * moveDistance;
        }

        if (Input.GetKey(KeyCode.E))
        {
            player.transform.Rotate(0, rotationY, 0);
        }

        if (Input.GetKey(KeyCode.Q))
        {
            player.transform.Rotate(0, -rotationY, 0);
        }
    }
}
