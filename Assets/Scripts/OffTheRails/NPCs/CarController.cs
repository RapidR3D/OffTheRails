using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OffTheRails.NPCs
{
	public class CarController : MonoBehaviour {

		Rigidbody2D rb;

		[SerializeField]
		float accelerationPower = 5f;
		[SerializeField]
		float steeringPower = 5f;
		public float speed;
		private float direction;
		public static float carSpeed = 0;
		[SerializeField] private GameObject controls2;
		[SerializeField] private Slider carGasSlider;
		[SerializeField] private GameObject rearLights;
		[SerializeField] private GameObject breaklights;
		private float rotation = 1;
		private bool isPointerDown = false;
		private AudioSource carSound;
		public float totalDistance;
		void Start () 
		{
			rb = GetComponent<Rigidbody2D> ();
			carSound = transform.Find("CarSound").GetComponent<AudioSource> ();
			carSound.Play ();
			totalDistance = 0;
		}

		void FixedUpdate()
		{
			speed = carSpeed * accelerationPower;
			carSound.pitch = 0.5f + Mathf.Abs(speed) / 10;

			if (speed == 0) breaklights.SetActive(false);
			if (speed < 0 && isPointerDown)
			{
				rearLights.SetActive(true);
			}
			else
			{
				rearLights.SetActive(false);
			}
			
			float deltaDistance = speed * Time.fixedDeltaTime;
			Vector2 newPos = rb.transform.position + rb.transform.up * deltaDistance;
			totalDistance += Mathf.Abs(deltaDistance);
			rb.MovePosition(newPos);

			direction = Mathf.Sign(Vector2.Dot(rb.linearVelocity, rb.GetRelativeVector(Vector2.up)));
			//rb.rotation += -Vars.steeringWheelAxis * steeringPower * speed * direction * Time.fixedDeltaTime * 60 * rotation;
		}

		public void OnCarGasSliderValueChange()
		{
			carSpeed = carGasSlider.value;
		}

		public void OnCarSliderPointerDown()
		{
			isPointerDown = true;
			breaklights.SetActive(false);
		}

		public void OnCarSliderPointerUp()
		{
			if(speed > 0) breaklights.SetActive(true);
			rearLights.SetActive(false);
			isPointerDown = false;
		}

		void OnCollisionEnter2D(Collision2D col)
		{
			rotation = 0;
		}

		void OnCollisionExit2D(Collision2D col)
		{
			rotation = 1;
		}
	}
}
