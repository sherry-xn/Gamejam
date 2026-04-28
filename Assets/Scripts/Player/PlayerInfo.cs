using System;
using UnityEngine;

[Serializable]
public class PlayerInfo
{
    [field: SerializeField] public int MaxHealth { get; private set; } = 2;
    [field: SerializeField] public int MaxKey { get; private set; } = 3;

    // Start is called before the first frame update
    // void Start()
    // {
    // currentHealth = maxHealth;
    //     healthBar.SetMaxHealth(maxHealth);
    // }

    // // Update is called once per frame
    // void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.Space))
    //     {
    //         TakeDamage(20);
    //     }
    // }
    // void TakeDamage(int damage)
    // {
    //     currentHealth -= damage;
    //     healthBar.SetHealth(currentHealth);
    // }
}
