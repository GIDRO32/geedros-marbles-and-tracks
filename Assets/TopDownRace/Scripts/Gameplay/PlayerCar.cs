using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace TopDownRace
{
    public class PlayerCar : MonoBehaviour
    {

        [HideInInspector]
        public float m_Speed;

        [HideInInspector]
        public int m_CurrentCheckpoint;


        [HideInInspector]
        public bool m_Control = false;

        public static PlayerCar m_Current;

        void Awake()
        {
            m_Current = this;
        }
        // Start is called before the first frame update
        void Start()
        {
            m_CurrentCheckpoint = 1;
            m_Control = true;
            m_Speed = 80;
        }

        // Update is called once per frame
        void Update()
        {
            float vertical = Input.GetAxisRaw("Vertical");
            float horizontal = Input.GetAxisRaw("Horizontal");

            Vector2 movementDirection = Helper.ToVector2(InputControl.m_Main.m_Movement);
            //movementDirection.Normalize();

            if (GameControl.m_Current.m_StartRace)
            {
                if (m_Control)
                {
                    GetComponent<CarPhysics>().m_InputAccelerate = movementDirection.magnitude;

                    if (movementDirection.magnitude > 0.1f)
                    {
                        float delta = Vector3.SignedAngle(movementDirection, transform.right, Vector3.forward);
                        if (GetComponent<CarPhysics>().m_InputAccelerate < 0)
                        {
                            GetComponent<CarPhysics>().m_InputSteer = Mathf.Clamp(.1f * delta, -1, 1);
                        }
                        else
                        {
                            GetComponent<CarPhysics>().m_InputSteer = -Mathf.Clamp(.1f * delta, -1, 1);
                        }
                    }
                    //if (GetComponent<CarPhysics>().m_InputAccelerate < 0)
                    //{
                    //    GetComponent<CarPhysics>().m_InputSteer = movementDirection.x;
                    //}
                    //else
                    //{
                    //    GetComponent<CarPhysics>().m_InputSteer = -movementDirection.x;
                    //}
                }
            }



        }

    }

}
