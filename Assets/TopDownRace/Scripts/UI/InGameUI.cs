using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


namespace TopDownRace
{
    public class InGameUI : MonoBehaviour
    {

        [SerializeField]
        private Text m_LevelRounds;
        [SerializeField]
        private Text m_FinishedRounds;

        public Text m_Timer;
        public Text m_Position;

        public static InGameUI Current;


        void Awake()
        {
            Current = this;

        }

        void Start()
        {

        }

        void Update()
        {

            m_FinishedRounds.text = GameControl.m_Current.m_FinishedLaps.ToString();
            m_LevelRounds.text = GameControl.m_Current.m_levelRounds.ToString();
            m_Timer.text = GameControl.m_Current.m_StartTimer.ToString();
            m_Position.text = (GameControl.m_Current.m_PlayerPosition + 1).ToString() + "th / 4";

            if (GameControl.m_Current.m_StartRace)
            {
                m_Timer.gameObject.SetActive(false);
            }

        }


        public void BtnPause()
        {
            //m_GameplayData.m_PowerIngameUIButton = false;
            //m_GameplayData.m_GameMode = 0;
            //GameControl.Current.PauseGame();
        }




    }

}
