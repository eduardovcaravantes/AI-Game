using UnityEngine;

namespace Stylized_3D_Chests_Pack___Animated_Collection.Resources.Demo
{
    public class DemoManager : MonoBehaviour
    {
        [SerializeField] private GameObject[] chestGameObjects;
        [SerializeField] private string url = "https://google.com";
        private int _chestParentIndex;
        private static readonly int NextKey = Animator.StringToHash("Next");

        private void Awake()
        {
            SetChestParent();
        }


        public void Open()
        {
            Application.OpenURL(url);
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                PreviousChestParent();
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                NextChestParent();
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                SetAnimation();
            }
        }

        private void SetChestParent()
        {
            for (int i = 0; i < chestGameObjects.Length; i++)
            {
                chestGameObjects[i].gameObject.SetActive(i==_chestParentIndex);
            }
        }

        private void SetAnimation()
        {
            Animator[] animators = chestGameObjects[_chestParentIndex].gameObject.GetComponentsInChildren<Animator>();
            foreach (var t in animators)
            {
                t.SetTrigger(NextKey);
            }
        }

        private void NextChestParent()
        {
            _chestParentIndex++;
            if (_chestParentIndex >= chestGameObjects.Length)
            {
                _chestParentIndex = 0;
            }

            SetChestParent();
        }

        private void PreviousChestParent()
        {
            _chestParentIndex--;
            if (_chestParentIndex < 0)
            {
                _chestParentIndex = chestGameObjects.Length - 1;
            }

            SetChestParent();
        }
    }
}