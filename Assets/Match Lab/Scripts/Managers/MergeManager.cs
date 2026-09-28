using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MergeManager : MonoBehaviour
{
    // Timeline (seconds from merge start, divided by mergeSpeedMultiplier):
    // the left and middle slots dip, the items leave one after another, the
    // outer two pull outward then slam into the raised middle item and vanish,
    // the middle swells and pops at finishTime.
    // World axes: +Y toward the camera, +Z up on screen, X along the rack.

    [Header(" Timing ")]
    [SerializeField] private float mergeSpeedMultiplier = 1f;
    [Tooltip("Pop: particles, items destroyed, rack starts compacting.")]
    [SerializeField] private float finishTime = .5f;

    [Header(" Outer Items ")]
    [Tooltip("Outward pull (away from the middle) before flying in.")]
    [SerializeField] private float windUpDistance = .22f;
    [Tooltip("Hop toward the camera before diving to the meeting point.")]
    [SerializeField] private float outerHopY = .10f;
    [Tooltip("Hop up the screen before diving to the meeting point.")]
    [SerializeField] private float outerHopZ = .38f;

    [Header(" Middle Item ")]
    [SerializeField] private float middleLiftY = .094f;
    [SerializeField] private float middleLiftZ = .343f;
    [SerializeField] private float middleSettleY = -.0045f;
    [SerializeField] private float middleSettleZ = -.016f;
    [SerializeField] private float swellScale = 1.5f;

    [Header(" Effects ")]
    [SerializeField] private ParticleSystem mergeParticles;

    [Header(" Actions ")]
    public static Action mergeCompleted;

    // Per outer item: leave, wind-up end, x arrival, hop end, y/z arrival, vanish.
    private static readonly float[] leftTimes = { .083f, .183f, .367f, .233f, .350f, .367f };
    private static readonly float[] rightTimes = { .133f, .200f, .333f, .233f, .317f, .333f };

    private const float LeftDipTime = 0f;
    private const float MiddleDipTime = .05f;
    private const float MiddleLeaveTime = .133f;
    private const float MiddleLiftEnd = .267f;
    private const float MiddleSettleEnd = .300f;
    private const float SwellStart = .283f;
    private const float SwellEnd = .450f;
    private const float ShrinkEnd = .550f;
    private const float SoundTime = .383f;

    // Merges never overlap today (the rack is busy during one), but a second
    // one is queued rather than interleaved, just in case.
    private readonly Queue<List<Item>> pendingMerges = new Queue<List<Item>>();
    private bool isMerging;

    private void Awake()
    {
        ItemSpotsManager.mergeStarted += OnMergeStarted;
    }

    private void OnDestroy()
    {
        ItemSpotsManager.mergeStarted -= OnMergeStarted;
    }

    private void OnMergeStarted(List<Item> items)
    {
        pendingMerges.Enqueue(new List<Item>(items));

        if (!isMerging)
            StartCoroutine(RunMerges());
    }

    private IEnumerator RunMerges()
    {
        isMerging = true;

        while (pendingMerges.Count > 0)
            yield return MergeSequence(pendingMerges.Dequeue());

        isMerging = false;
    }

    private IEnumerator MergeSequence(List<Item> items)
    {
        items.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        Item left = items[0], middle = items[1], right = items[2];

        ItemSpot leftSpot = left.Spot, middleSpot = middle.Spot;
        float middleX = middle.transform.position.x;

        OuterState leftState = new OuterState(left, leftTimes, -1);
        OuterState rightState = new OuterState(right, rightTimes, 1);

        Vector3 middleBase = Vector3.zero, meetingPoint = middle.transform.position;
        Vector3 middleScale = Vector3.one;
        bool middleLeft = false, leftDipped = false, middleDipped = false, soundPlayed = false;
        float time = 0;

        while (time < finishTime)
        {
            if (!leftDipped && time >= LeftDipTime) { leftDipped = true; if (leftSpot != null) leftSpot.PlayJump(); }
            if (!middleDipped && time >= MiddleDipTime) { middleDipped = true; if (middleSpot != null) middleSpot.PlayJump(); }

            // Middle: lifts to the meeting point, settles, swells, shrinks.
            if (!middleLeft && time >= MiddleLeaveTime && middle != null)
            {
                middleLeft = true;
                Detach(middle);
                middleBase = middle.transform.position;
                middleScale = middle.transform.localScale;
                meetingPoint = middleBase + new Vector3(0, middleLiftY + middleSettleY, middleLiftZ + middleSettleZ);
            }

            if (middleLeft && middle != null)
            {
                Vector3 lifted = middleBase + new Vector3(0, middleLiftY, middleLiftZ);
                middle.transform.position = time < MiddleLiftEnd
                    ? Vector3.LerpUnclamped(middleBase, lifted, EaseOutSine(Segment(time, MiddleLeaveTime, MiddleLiftEnd)))
                    : Vector3.LerpUnclamped(lifted, meetingPoint, EaseInSine(Segment(time, MiddleLiftEnd, MiddleSettleEnd)));

                float scale = time < SwellEnd
                    ? Mathf.Lerp(1, swellScale, EaseOutSine(Segment(time, SwellStart, SwellEnd)))
                    : Mathf.Lerp(swellScale, 0, EaseInSine(Segment(time, SwellEnd, ShrinkEnd)));
                middle.transform.localScale = middleScale * scale;
            }

            leftState.Update(this, time, middleX, meetingPoint, middleLeft);
            rightState.Update(this, time, middleX, meetingPoint, middleLeft);

            if (!soundPlayed && time >= SoundTime)
            {
                soundPlayed = true;
                // TODO: merge sound once there is an audio system.
            }

            yield return null;
            time += Time.deltaTime * mergeSpeedMultiplier;
        }

        FinishMerge(items, meetingPoint);
    }

    // Pop: particles at the meeting point, items gone, rack starts sliding now.
    private void FinishMerge(List<Item> items, Vector3 meetingPoint)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null)
                continue;

            LeanTween.cancel(items[i].gameObject);
            Destroy(items[i].gameObject);
        }

        if (mergeParticles != null)
            Instantiate(mergeParticles, meetingPoint, Quaternion.identity, transform).Play();

        // TODO: merge haptic once there is an audio/haptics system.
        mergeCompleted?.Invoke();
    }

    // Off the slot, keeping the world pose, so later slot motion doesn't drag it.
    private void Detach(Item item)
    {
        LeanTween.cancel(item.gameObject);
        item.transform.SetParent(transform, true);
    }

    private class OuterState
    {
        private readonly Item item;
        private readonly float[] times;
        private readonly float side;
        private bool left;
        private bool hidden;
        private Vector3 start;

        public OuterState(Item item, float[] times, float side)
        {
            this.item = item;
            this.times = times;
            this.side = side;
        }

        public void Update(MergeManager manager, float time, float middleX, Vector3 meetingPoint, bool meetingKnown)
        {
            if (item == null || hidden || time < times[0])
                return;

            if (!left)
            {
                left = true;
                manager.Detach(item);
                start = item.transform.position;
            }

            if (time >= times[5])
            {
                hidden = true;
                item.Hide();
                return;
            }

            // X: pull outward, then accelerate into the middle.
            float outX = start.x + side * manager.windUpDistance;
            float x = time < times[1]
                ? Mathf.Lerp(start.x, outX, EaseOutSine(Segment(time, times[0], times[1])))
                : Mathf.Lerp(outX, middleX, EaseInSine(Segment(time, times[1], times[2])));

            // Y/Z: hop up, then dive onto the meeting point.
            Vector3 hop = start + new Vector3(0, manager.outerHopY, manager.outerHopZ);
            Vector3 yz = time < times[3] || !meetingKnown
                ? Vector3.Lerp(start, hop, EaseOutSine(Segment(time, times[0], times[3])))
                : Vector3.Lerp(hop, meetingPoint, EaseInSine(Segment(time, times[3], times[4])));

            item.transform.position = new Vector3(x, yz.y, yz.z);
        }
    }

    private static float Segment(float time, float from, float to)
        => Mathf.Clamp01((time - from) / (to - from));

    private static float EaseOutSine(float t) => Mathf.Sin(t * Mathf.PI * .5f);

    private static float EaseInSine(float t) => 1 - Mathf.Cos(t * Mathf.PI * .5f);
}
