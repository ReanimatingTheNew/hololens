﻿/*
 * Requirements:
 * -------------
 * - Attach this script to the top-level object.
 * - A sub-object named "Destroyed" should contain children whose names match
 *   other sub-objects parented to the top-level node. These are wreckage parts
 *   that correspond to each "live" part. Any Rigidbodies attached to wreckage
 *   will be enabled on destruction and the top-level Rigidbody will be
 *   disabled.
 * - projectileLayer: Set this to the projectile layer and make sure all
 *   objects in that layer implement IProjectile.
 */

using System.Collections.Generic;
using UnityEngine;

public class Destructible : MonoBehaviour
{
  [Tooltip("Health is how many hit points of damage this object can take before being destroyed.")]
  public int healthPoints = 10;

  [Tooltip("Which layer projectile objects that can damage us are in. These must all implement IProjectile.")]
  public LayerMask projectileLayer;

  [Tooltip("Bullet impact particle effect.")]
  public ParticleSystem bulletImpactPrefab;

  [Tooltip("Bullet impact sound clips.")]
  public AudioClip[] bulletImpactClips;

  [Tooltip("Explosion velocity (m/s)")]
  public float explosionVelocity = 2.5f;

  [Tooltip("Explosions when destroyed (a random one will be selected).")]
  public GameObject[] explosionPrefabs;

  [Tooltip("Explosion sound clips.")]
  public AudioClip[] explosionClips;

  public bool Alive
  {
    get
    {
      return m_alive;
    }
  }

  private AudioSource m_audio;
  private GameObject[] m_explosions;
  private bool m_destroyed = false;
  private Dictionary<GameObject, GameObject> m_wreckageByPart = new Dictionary<GameObject, GameObject>();
  private bool m_alive = true;

  public Vector3 GetAimPoint()
  {
    BoxCollider box = GetComponent<BoxCollider>();
    if (box != null)
      return transform.TransformPoint(box.center);
    CapsuleCollider capsule = GetComponent<CapsuleCollider>();
    if (capsule != null)
      return transform.TransformPoint(capsule.center);
    return transform.position;
  }

  private void InitWreckage()
  {
    Transform wreckageNode = transform.Find("Destroyed");
    if (wreckageNode == null)
    {
      Debug.Log("ERROR: No Destroyed sub-object found.");
      return;
    }

    // Find all wreckage parts
    Dictionary<string, GameObject> wreckageByName = new Dictionary<string, GameObject>();
    foreach (Transform child in wreckageNode.GetComponentsInChildren<Transform>())
    {
      wreckageByName[child.name] = child.gameObject;
    }

    // Associate each live component with a piece of wreckage if it exists
    foreach (Transform child in GetComponentsInChildren<Transform>())
    {
      GameObject part = child.gameObject;
      if (!wreckageByName.ContainsValue(part))
      {
        // Found something that is not wreckage, see if we can associate it
        // with a piece of wreckage (which should be named identically)
        GameObject wreckagePart;
        if (wreckageByName.TryGetValue(part.name, out wreckagePart))
          m_wreckageByPart[part] = wreckagePart;
      }
    }
  }

  private void SelfDestruct(float delay)
  {
    //TODO: disable NavMesh agent?
    m_alive = false;

    // Disable NavMeshAgents
    foreach (UnityEngine.AI.NavMeshAgent agent in GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())
    {
      agent.enabled = false;
    }

    // Enable all sub-object Rigidbodies (make them obey gravity and not be
    // kinematic)
    foreach (Rigidbody crb in GetComponentsInChildren<Rigidbody>())
    {
      crb.isKinematic = false;
      crb.useGravity = true;
    }

    // Disable top-level Rigidbody if it exists
    Rigidbody rb = GetComponent<Rigidbody>();
    if (rb != null)
      rb.isKinematic = true;
    
    // Turn off all scripts
    foreach (MonoBehaviour behavior in GetComponentsInChildren<MonoBehaviour>())
    {
      behavior.enabled = false;
    }

    // Disable all sub-objects
    foreach (Transform child in GetComponentsInChildren<Transform>())
    {
      if (child.gameObject == gameObject)
        continue; // do not disable top-level object!
      child.gameObject.SetActive(false);
    }

    // Re-enable wreckage and position each piece the same way that the live
    // counterpart was before exploding it :)
    Transform wreckageNode = transform.Find("Destroyed");
    if (wreckageNode != null)
      wreckageNode.gameObject.SetActive(true);
    foreach (KeyValuePair<GameObject, GameObject> v in m_wreckageByPart)
    {
      GameObject part = v.Key;
      GameObject wreckage = v.Value;
      wreckage.transform.position = part.transform.position;
      wreckage.transform.rotation = part.transform.rotation;
      wreckage.SetActive(true);

      // Kaboom!
      Rigidbody wrb = wreckage.GetComponent<Rigidbody>();
      if (wrb != null)
        wrb.AddExplosionForce(explosionVelocity, transform.position, 0, 0, ForceMode.VelocityChange);
    }

    //SelfDestruct destructor = GetComponent<SelfDestruct>();
    //destructor.time = delay;
    //destructor.enabled = true;
    //gameObject.SetActive(false);
  }

/*
  // Make sure to keep this script enabled after destruction for this to work
  private void FixedUpdate()
  {
    List<string> s = new List<string>();
    foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
    {
      s.Add(rb.name + "=" + rb.velocity.ToString("F2"));
    }
    Debug.Log("Velocities: " + string.Join(", ", s.ToArray()));
  }
*/

  private void OnCollisionEnter(Collision collision)
  {
    if (m_destroyed)
      return;

    GameObject collided = collision.collider.gameObject;
    int collidedLayer = 1 << collided.layer;
    if ((collidedLayer & projectileLayer) == 0)
      return;

    IProjectile projectile = collided.GetComponent<IProjectile>();
    if (projectile == null)
      return;

    healthPoints -= projectile.HitPoints;
    if (healthPoints <= 0)
    {
      m_destroyed = true;
      healthPoints = 0;
      int random = Random.Range(0, explosionPrefabs.Length - 1);
      if (explosionPrefabs.Length > 0)
      {
        m_explosions[random].transform.position = transform.position;
        m_explosions[random].transform.rotation = transform.rotation;
        m_explosions[random].SetActive(true);
      }
      random = Random.Range(0, explosionClips.Length - 1);
      m_audio.Stop();
      m_audio.PlayOneShot(explosionClips[random]);
      SelfDestruct(explosionClips[random].length);
    }
    else if (bulletImpactPrefab != null)
    {
      Instantiate(bulletImpactPrefab, collision.contacts[0].point, Quaternion.identity);
      int random = Random.Range(0, bulletImpactClips.Length - 1);
      m_audio.Stop();
      m_audio.PlayOneShot(bulletImpactClips[random]);
    }
  }

  private void Awake()
  {
    m_audio = GetComponent<AudioSource>();
    m_explosions = new GameObject[explosionPrefabs.Length];
    for (int i = 0; i < explosionPrefabs.Length; i++)
    {
      m_explosions[i] = Instantiate(explosionPrefabs[i]);
      m_explosions[i].SetActive(false);
    }
    InitWreckage();
  }
}