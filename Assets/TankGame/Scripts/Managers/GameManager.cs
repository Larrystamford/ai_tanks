using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

namespace Complete
{
    public class GameManager : MonoBehaviour
    {
        public int m_NumRoundsToWin = 5;            // The number of rounds a single player has to win to win the game.
        public int m_RoundNumber = 1;                  // Which round the game is currently on.
        public float m_StartDelay = 1f;             // The delay between the start of RoundStarting and RoundPlaying phases.
        public float m_EndDelay = 3f;               // The delay between the end of RoundPlaying and RoundEnding phases.
        public CameraControl m_CameraControl;       // Reference to the CameraControl script for control during different phases.
        public Text m_MessageText;                  // Reference to the overlay Text to display winning text, etc.
        public GameObject m_TankPrefab;             // Reference to the prefab the players will control.
        public GameObject m_AiTankPrefab;             // Reference to the prefab the players will control.
        public TankManager[] m_Tanks;               // A collection of managers for enabling and disabling different aspects of the tanks.
        public AiTankManager[] m_Ai_Tanks;               // A collection of managers for enabling and disabling different aspects of the tanks.


        private WaitForSeconds m_StartWait;         // Used to have a delay whilst the round starts.
        private WaitForSeconds m_EndWait;           // Used to have a delay whilst the round or game ends.
        private TankManager m_GameWinner;           // Reference to the winner of the game.  Used to make an announcement of who won.


        private bool playerWonRound;
        private int totalPoints = 0;

        private void Start()
        {
            // Create the delays so they only have to be made once.
            m_StartWait = new WaitForSeconds (m_StartDelay);
            m_EndWait = new WaitForSeconds (m_EndDelay);

            SpawnAllTanks();
            SetCameraTargets();

            // Once the tanks have been created and the camera is using them as targets, start the game.
            StartCoroutine (GameLoop ());
        }


        private void SpawnAllTanks()
        {
            // For all the tanks...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... create them, set their player number and references needed for control.
                m_Tanks[i].m_Instance =
                    Instantiate(m_TankPrefab, m_Tanks[i].m_SpawnPoint.position, m_Tanks[i].m_SpawnPoint.rotation) as GameObject;
                m_Tanks[i].m_PlayerNumber = i + 1;
                m_Tanks[i].Setup();
            }
        }


        private void SetCameraTargets()
        {
            // Create a collection of transforms the same size as the number of tanks.
            Transform[] targets = new Transform[m_Tanks.Length];

            // For each of these transforms...
            for (int i = 0; i < targets.Length; i++)
            {
                // ... set it to the appropriate tank transform.
                targets[i] = m_Tanks[i].m_Instance.transform;
            }

            // These are the targets the camera should follow.
            m_CameraControl.m_Targets = targets;
        }


        // This is called from start and will run each phase of the game one after another.
        private IEnumerator GameLoop ()
        {
            // Start off by running the 'RoundStarting' coroutine but don't return until it's finished.
            yield return StartCoroutine (RoundStarting ());

            // Once the 'RoundStarting' coroutine is finished, run the 'RoundPlaying' coroutine but don't return until it's finished.
            yield return StartCoroutine (RoundPlaying());

            // Once execution has returned here, run the 'RoundEnding' coroutine, again don't return until it's finished.
            yield return StartCoroutine (RoundEnding());

            if (!playerWonRound)
            {
                // If player lost, restart the level.
                m_RoundNumber = 1;
                SceneManager.LoadScene(0);
            }
            else
            {
                // If there isn't a winner yet, restart this coroutine so the loop continues.
                // Note that this coroutine doesn't yield.  This means that the current version of the GameLoop will end.
                StartCoroutine (GameLoop ());
            }
        }


        private IEnumerator RoundStarting ()
        {
            for (int i = 0; i < m_Ai_Tanks.Length; i++)
            {
                // ... create them, set their player number and references needed for control.
                m_Ai_Tanks[i].m_Instance =
                    Instantiate(m_AiTankPrefab, m_Ai_Tanks[i].m_SpawnPoint.position, m_Ai_Tanks[i].m_SpawnPoint.rotation) as GameObject;
                m_Ai_Tanks[i].m_PlayerNumber = i + 1;

                float ai_speed = 1;
                float ai_health = 10;
                float ai_reload_time = 10;
                m_Ai_Tanks[i].Setup(ai_speed * m_RoundNumber, ai_health * m_RoundNumber, ai_reload_time - m_RoundNumber);

                // m_Ai_Tanks[i].m_Movement.m_Speed = 1;
            }

            // As soon as the round starts reset the tanks and make sure they can't move.
            ResetAllTanks ();
            DisableTankControl ();

            // Snap the camera's zoom and position to something appropriate for the reset tanks.
            m_CameraControl.SetStartPositionAndSize ();

            // Increment the round number and display text showing the players what round it is.
            m_MessageText.text = "ROUND " + m_RoundNumber;
            m_RoundNumber++;

            // Wait for the specified length of time until yielding control back to the game loop.
            yield return m_StartWait;
        }


        private IEnumerator RoundPlaying ()
        {
            // As soon as the round begins playing let the players control the tanks.
            EnableTankControl ();

            // Clear the text from the screen.
            m_MessageText.text = string.Empty;


            // While there is not one tank left...
            while (!RoundHasEnded())
            {
                // ... return on the next frame.
                yield return null;
            }
        }

        private bool RoundHasEnded()
        {
            int numAiTanksLeft = 0;
            int numTanksLeft = 0;

            for (int i = 0; i < m_Ai_Tanks.Length; i++)
            {
                if (m_Ai_Tanks[i].m_Instance.activeSelf)
                    numAiTanksLeft++;
            }

            for (int i = 0; i < m_Tanks.Length; i++)
            {
                if (m_Tanks[i].m_Instance.activeSelf)
                    numTanksLeft++;
            }

            if (numAiTanksLeft == 0) {
                playerWonRound = true;
                return true;
            }
            if (numTanksLeft == 0) {
                playerWonRound = false;
                return true;
            }

            return false;
        }


        private IEnumerator RoundEnding ()
        {
            // Stop tanks from moving.
            DisableTankControl ();


            // Get a message based on the scores and whether or not there is a game winner and display it.
            string message = EndMessage(playerWonRound);
            m_MessageText.text = message;

            // Wait for the specified length of time until yielding control back to the game loop.
            yield return m_EndWait;
        }



        // Returns a string message to display at the end of each round.
        private string EndMessage(bool playerWon)
        {
            int roundPoints = (m_RoundNumber-1)*1000;
            string message = "GAME OVER.\nYOUR TOTAL POINTS ARE: " + totalPoints.ToString();
            if (playerWon) {
                message = "ALL ENEMIES DESTROYED!\n+" + roundPoints.ToString() + " POINTS" + "\nGET READY FOR THE NEXT ROUND!";
            }
            totalPoints += roundPoints;

            return message;
        }


        // This function is used to turn all the tanks back on and reset their positions and properties.
        private void ResetAllTanks()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].Reset();
            }
            for (int i = 0; i < m_Ai_Tanks.Length; i++)
            {
                m_Ai_Tanks[i].Reset();
            }
        }

        private void EnableTankControl()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].EnableControl();
            }
        }

        private void DisableTankControl()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].DisableControl();
            }
        }
    }
}