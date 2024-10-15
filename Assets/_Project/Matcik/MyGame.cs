using UnityEngine;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
public class MyGame : MonoBehaviour
{
    public Entity potionPrefab;
    public Entity zombiePrefab;
    public Entity player;
    public float potionSpawnInterval;
    public float zombieSpawnInterval;
    public float potionSpawnT;
    public float zombieSpawnT;

    public List<Entity> entities = new(256);

    public void Start()
    {
        player.transform.position = new Vector3(0, 0, -2);
        entities.Add(player);
    }

    public void Update()
    {
        UpdateInput();
        UpdatePotions();
        UpdateZombies();
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
                // TODO(sqd): Randomize speed
                zombies.Add(zombie);
            }
        }


        for (int i = 0; i < zombies.Count; i++)
        {
            // NOTE(sqd): Update every zombie           
            Entity zombie = zombies[i];

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

    public void Die(Entity player)
    {
        Debug.LogWarning("You are dead");
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
