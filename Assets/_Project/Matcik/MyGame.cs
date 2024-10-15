using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
public class MyGame : MonoBehaviour
{
    public HealPotion potionPrefab;
    public Zombie zombiePrefab;
    public Player player;
    public float potionSpawnT;
    public float zombieSpawnT;  
 

    public List<HealPotion> potions = new(10);
    public List<Zombie> zombies = new(10);

    public void Start()
    {
        player.transform.position = new Vector3(0, 0, -2);
    }

    // Healed zombies search for potions too

    public void Update()
    {
        UpdateInput();
        UpdatePotions();
        UpdateZombies();
        
    }

    public void UpdateZombies()
    {
        zombieSpawnT -= Time.deltaTime;

        if (zombieSpawnT <= 0 && zombies.Count < 10)
        {
            zombieSpawnT += 1;
            Zombie zombie = SpawnZombie();
            zombies.Add(zombie);                  
        }
        else if (zombies.Count >= 10)
        {           
            zombieSpawnT = 1;
        }

        for (int i = 0; i < zombies.Count; i++)
        {
            // NOTE(sqd): Update every zombie           
            Zombie zombie = zombies[i];


            if (zombie.isHealed && zombie.hasPotion)
            {
                HealOtherZombies(zombie);
            }
            else
            {
                if(!zombie.hasPotion && zombie.isHealed)
        {
                    HealPotion nearestPotion = FindNearestPotion(zombie);                                                                                          
                // Поиск ближайшего зелья                                                                                   
               // Если найдено ближайшее зелье, зомби двигается к нему
            
            if (nearestPotion != null)
            {
                zombie.transform.LookAt(nearestPotion.transform);
                float moveDistance = zombie.speed * Time.deltaTime;
                zombie.transform.position += zombie.transform.forward * moveDistance;

                // Если зомби достигает зелья, оно уничтожается
                if (Vector3.Distance(zombie.transform.position, nearestPotion.transform.position) < 1.5f)
                {
                    GameObject.Destroy(nearestPotion.gameObject);
                    potions.Remove(nearestPotion);
                    zombie.hasPotion = true;
                }
            }
        }           
            else
            {
                zombie.transform.LookAt(player.transform);

                float moveDistance = zombie.speed * Time.deltaTime;
                zombie.transform.position += zombie.transform.forward * moveDistance;

                // NOTE(sqd): Check if player near by
                if (Vector3.Distance(player.transform.position, zombie.transform.position) < 3)
                {
                    if (player.potionsCount > 0)
                    {
                        player.potionsCount--;
                        zombie.isHealed = true;
                        zombie.mr.sharedMaterial = zombie.healedMat;
                    }
                    else
                    {
                        Die(player);
                    }
                }
            }
        }
    }
}
        
    

    public void Die(Player player)
    {
        Debug.LogWarning("You are dead");
    }

    public void UpdatePotions()
    {
        // NOTE(sqd): Spawn potions
        potionSpawnT -= Time.deltaTime;

        if (potionSpawnT <= 0 && potions.Count < 10)
        {
            potionSpawnT += 1;
            HealPotion potion = SpawnPotion();
            potions.Add(potion);
        }
        else if (potions.Count >= 10)
        {            
            potionSpawnT = 1;
        }

        // NOTE(sqd): Rotate potions over time
        for (int i = 0; i < potions.Count; i++)
        {
            potions[i].transform.Rotate(0, 30 * Time.deltaTime, 0);

            // NOTE(sqd): Check if player near by
            if (Vector3.Distance(player.transform.position, potions[i].transform.position) < 5)
            {
                GameObject.Destroy(potions[i].gameObject);
                player.potionsCount++;
                potions.RemoveAt(i);
                i--;
            }
        }
    }

    public HealPotion SpawnPotion()
    {
        float randomX = Random.Range(-100, 100);
        float randomZ = Random.Range(-100, 100);
        Vector3 randomPosition = new Vector3(randomX, 0, randomZ);

        HealPotion healPotion = Instantiate(potionPrefab, randomPosition, Quaternion.identity);

        return healPotion;
    }

    public Zombie SpawnZombie()
    {
        float randomX = Random.Range(-100, 100);
        float randomZ = Random.Range(-100, 100);
        Vector3 randomPosition = new Vector3(randomX, 0, randomZ);

        Zombie zombie = Instantiate(zombiePrefab, randomPosition, Quaternion.identity);

        return zombie;
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
    public HealPotion FindNearestPotion(Zombie zombie)
    {
        HealPotion nearestPotion = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < potions.Count; i++)
        {
            float distance = Vector3.Distance(zombie.transform.position, potions[i].transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPotion = potions[i];
            }
        }

        return nearestPotion;
    }
    public void HealOtherZombies(Zombie healedZombie)
    {
        for (int i = 0; i < zombies.Count; i++)
        {
            Zombie otherZombie = zombies[i];
            if (!otherZombie.isHealed)
            {
                float distance = Vector3.Distance(healedZombie.transform.position, otherZombie.transform.position);
                if (distance < 3) // Range within which a healed zombie can heal another one
                {
                    otherZombie.isHealed = true;
                    otherZombie.mr.sharedMaterial = otherZombie.healedMat;
                }
            }
        }
    }



}
