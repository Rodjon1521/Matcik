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

    public List<Entity> entities = new(256);

    public void Start()
    {
        player.transform.position = new Vector3(0, 1, -2);
        entities.Add(player);
    }

    public void Update()
    {
        if (inputEnabled == true && !hasFallen)
        {
            UpdateInput();
        }

        if (!inputEnabled && !hasFallen)
        {
            NewInfectedInput();
        }

        UpdatePotions();
        UpdateZombies();
        UpdateInfectionTimer();
        Healing();
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
                Entity zombie = SpawnEntity(zombiePrefab, 100f);
                zombies.Add(zombie);
            }
        }

        for (int i = 0; i < zombies.Count; i++)
        {
            Entity zombie = zombies[i];


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
                    // Вектор направления, куда двигаться зомби
                    Vector3 moveDirection = Vector3.zero;

                    // Применение правил flocking behavior
                    Vector3 separation = Vector3.zero;
                    Vector3 alignment = Vector3.zero;
                    Vector3 cohesion = Vector3.zero;

                    // Параметры для регулировки силы эффектов
                    float separationDistance = 10.0f; // Минимальная дистанция между зомби
                    float alignmentWeight = 0f; // Влияние выравнивания
                    float cohesionWeight = 0f; // Влияние притяжения к соседям

                    // Найдём соседей для зомби
                    List<Entity> neighbors = new List<Entity>();
                    foreach (var entity in GetEntitiesOfType(EntityType.Zombie))
                    {
                        if (entity != zombie) // Убираем самого себя из списка соседей
                        {
                            neighbors.Add(entity);
                        }
                    }

                    foreach (var neighbor in neighbors)
                    {
                        float distanceToNeighbor =
                            Vector3.Distance(zombie.transform.position, neighbor.transform.position);

                        // Правило разделения: избегаем других зомби
                        if (distanceToNeighbor < separationDistance)
                        {
                            separation += (zombie.transform.position - neighbor.transform.position).normalized /
                                          distanceToNeighbor;
                        }

                        // Правило выравнивания: стараемся двигаться в том же направлении, что и соседи
                        alignment += neighbor.transform.forward;

                        // Правило притяжения (cohesion): стремимся к средней позиции соседей
                        cohesion += neighbor.transform.position;
                    }

                    if (neighbors.Count > 0)
                    {
                        // Учитываем среднюю позицию для притяжения
                        cohesion /= neighbors.Count;
                        cohesion = (cohesion - zombie.transform.position).normalized;

                        // Учитываем направление движения соседей
                        alignment = alignment.normalized;
                    }

                    // Итоговое направление с учётом всех правил
                    moveDirection = separation.normalized * separationDistance + alignment * alignmentWeight +
                                    cohesion * cohesionWeight;

                    // Следование за игроком (EntityToFollow) как основное поведение
                    moveDirection += (entityToFollow.transform.position - zombie.transform.position).normalized;

                    // Нормализуем вектор для соблюдения скорости
                    moveDirection = moveDirection.normalized;

                    // Поворачиваем зомби в сторону движения
                    zombie.transform.LookAt(zombie.transform.position + moveDirection);

                    // Движение зомби
                    float moveDistance = zombie.speed * Time.deltaTime;
                    zombie.transform.position += moveDirection * moveDistance;

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
                                player.isHealed = false;
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
                Entity potion = SpawnEntity(potionPrefab, 20f);
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

    public Entity SpawnEntity(Entity prefab, float minimumDistance = 70.0f)
    {
        Vector3 randomPosition;
        float distanceToPlayer;

        // Генерируем случайную позицию, пока она не будет на безопасном расстоянии от игрока
        do
        {
            float randomX = Random.Range(-100, 100);
            float randomZ = Random.Range(-100, 100);
            randomPosition = new Vector3(randomX, 0, randomZ);

            // Вычисляем расстояние до игрока
            distanceToPlayer = Vector3.Distance(randomPosition, player.transform.position);
        } while (distanceToPlayer < minimumDistance); // Повторяем, пока позиция слишком близка к игроку

        // Создаем зомби
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

    public void Healing()
    {
        if (isInfected && !player.isHealed && player.HasPotion())
        {
            isInfected = false;
            player.potionsCount--;
            player.isHealed = true;
            inputDisableTimer = 3f;
            inputEnabled = true;
            infectTimerT = 10f;
        }
    }

    public bool hasFallen = false;


    public void EnableFall()
    {
        player.GetComponent<Rigidbody>().isKinematic = false;
        player.GetComponent<Rigidbody>().useGravity = true;
        player.GetComponent<Rigidbody>().AddForce(Vector3.back * 5f, ForceMode.Impulse);
        player.GetComponent<AudioSource>().clip = player.deathSound;
        player.GetComponent<AudioSource>().Play();
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