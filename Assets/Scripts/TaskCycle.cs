using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TaskCycle : MonoBehaviour
{
    public enum TaskType
    {
        Phone,
        Typewriter
    }

    [Serializable]
    public struct Entry
    {
        [SerializeField] private TaskType type;
        [SerializeField] private Guest guest;
        [SerializeField] private GameObject rewardItem;
        [SerializeField] private DialogueObject dialogue;
        [SerializeField, Min(0f)] private float delayToNextTask;

        public TaskType Type => type;
        public Guest Guest => guest;
        public GameObject RewardItem => rewardItem;
        public DialogueObject Dialogue => dialogue;
        public float DelayToNextTask => delayToNextTask;
    }

    [SerializeField] private Entry[] tasks = Array.Empty<Entry>();

    [SerializeField] private bool repeat = false;
    [SerializeField] private GuestDoor guestDoor;

    public IReadOnlyList<Entry> Tasks => tasks;

    private void Awake()
    {
        repeat = false;
        foreach (Entry task in tasks)
        {
            Guest source = task.Guest;
            if (task.Type != TaskType.Typewriter || source == null) continue;

            GameObject reward = GetSceneReward(task);
            if (reward != null) reward.SetActive(false);
            if (source.gameObject.scene != gameObject.scene) continue;

            if (reward == null) source.LockReward();
            source.gameObject.SetActive(false);
        }
    }

    private GameObject GetSceneReward(Entry task)
    {
        GameObject reward = task.RewardItem;
        if (reward == null || reward.scene != gameObject.scene ||
            transform.IsChildOf(reward.transform) || task.Guest.transform.IsChildOf(reward.transform))
            return null;
        return reward;
    }

    private IEnumerator Start()
    {
        do
        {
            bool playedTask = false;
            foreach (Entry task in tasks)
            {
                Guest source = task.Guest;
                if (task.Type != TaskType.Typewriter || source == null || !source.HasOrder) continue;

                playedTask = true;
                Guest guest = Instantiate(source, source.transform.parent);
                guest.name = source.name;
                guest.PrepareArrival(GetSceneReward(task), guestDoor);
                guest.gameObject.SetActive(true);

                yield return new WaitUntil(() => guest == null);
                var phone = GetComponent<RoomPhoneCall>();
                if (phone == null) phone = gameObject.AddComponent<RoomPhoneCall>();
                yield return phone.RingAndWait();
                if (task.DelayToNextTask > 0f)
                    yield return new WaitForSeconds(task.DelayToNextTask);
            }

            if (!playedTask) yield break;
        } while (repeat);
    }
}
