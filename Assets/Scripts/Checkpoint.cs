using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TopDownRace
{
    public class Checkpoint : MonoBehaviour
    {

        public int m_ID;
        [HideInInspector]
        public bool isPassed;

        public bool isFinishLine;
    public enum CheckpointType {
        Normal,
        PitEntrance,
        PitExit
    }

    [Header("Checkpoint Settings")]
    public CheckpointType type = CheckpointType.Normal;
    public bool isPitStop = false;

    // The next checkpoint ID cars must reach after this one
    // public int nextCheckpointId;
        void Start()
        {
            isPassed = false;
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.tag == "Rival")
            {
                collision.gameObject.GetComponent<Rivals>().Checkpointing(m_ID);
            }
        }
    }
}