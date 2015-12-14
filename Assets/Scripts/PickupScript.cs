using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class PickupScript : MonoBehaviour {

    private IEnumerator Fade() {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        ParticleSystem particleSystem = GetComponent<ParticleSystem>();
        particleSystem.Stop();
        for (float a = 1.0f; a >= 0.0f; a -= 0.1f) {
            if (Mathf.Approximately(a, 0.0f)) a = 0.0f;
            Color color = spriteRenderer.color;
            color.a = a;
            spriteRenderer.color = color;
            yield return null;
        }

        
    }

    private IEnumerator DestroyAfterSound() {
        AudioSource audioSource = GetComponent<AudioSource>();
        while (audioSource.isPlaying) yield return null;
        GameObject.Destroy(gameObject);
    }

    public void ActivatePickup() {
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.Play();
        StartCoroutine("Fade");
        StartCoroutine("DestroyAfterSound");
    }
}
