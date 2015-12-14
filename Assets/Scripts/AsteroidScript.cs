using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class AsteroidScript : MonoBehaviour {

	void Start () {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.angularVelocity = 30.0f;

	}

}
