using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Game.Player
{
    /// <summary>Owner-scoped screen reservations for world interaction lettering.</summary>
    public static class InteractionPromptScreenReservations
    {
        private readonly struct Registration
        {
            public readonly Object Owner;
            public readonly Func<Rect?> Provider;

            public Registration(Object owner, Func<Rect?> provider)
            {
                Owner = owner;
                Provider = provider;
            }
        }

        private static readonly List<Registration> Registrations = new List<Registration>(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAtSubsystemRegistration()
        {
            Registrations.Clear();
        }

        public static void Register(Object owner, Func<Rect?> provider)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            for (int i = 0; i < Registrations.Count; i++)
            {
                if (!ReferenceEquals(Registrations[i].Owner, owner)) continue;
                Registrations[i] = new Registration(owner, provider);
                return;
            }
            Registrations.Add(new Registration(owner, provider));
        }

        public static void Unregister(Object owner)
        {
            // Reference identity also permits removal after Unity has destroyed the owner.
            if (ReferenceEquals(owner, null)) return;
            for (int i = Registrations.Count - 1; i >= 0; i--)
                if (ReferenceEquals(Registrations[i].Owner, owner)) Registrations.RemoveAt(i);
        }

        public static void CollectRects(List<Rect> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            for (int i = 0; i < Registrations.Count;)
            {
                var entry = Registrations[i];
                if (entry.Owner == null)
                {
                    Registrations.RemoveAt(i);
                    continue;
                }
                var rectangle = entry.Provider();
                if (rectangle.HasValue && IsValid(rectangle.Value)) destination.Add(rectangle.Value);
                i++;
            }
        }

        /// <summary>
        /// Fits only horizontally; original Y must already fit the padded viewport.
        /// Scratch holds ascending free X intervals and is cleared on every call.
        /// False leaves the rectangle and scale unchanged instead of inventing a vertical fallback.
        /// </summary>
        public static bool TryFitHorizontal(Rect prompt, Rect viewport, IReadOnlyList<Rect> blockers,
            float padding, List<Vector2> scratch, out Rect fitted, out float scale)
        {
            fitted = prompt;
            scale = 1f;
            if (scratch == null) throw new ArgumentNullException(nameof(scratch));
            scratch.Clear();
            if (!IsValid(prompt) || !IsValid(viewport) || !IsFinite(padding) || padding < 0f) return false;

            float left = viewport.xMin + padding;
            float right = viewport.xMax - padding;
            float bottom = viewport.yMin + padding;
            float top = viewport.yMax - padding;
            if (right <= left || top <= bottom || prompt.yMin < bottom || prompt.yMax > top) return false;
            scratch.Add(new Vector2(left, right));

            // Subtracting from ordered free intervals merges overlapping blockers implicitly.
            // No sorting delegate or temporary collection is needed in the frame path.
            if (blockers != null)
            {
                for (int i = 0; i < blockers.Count && scratch.Count > 0; i++)
                {
                    var blocker = blockers[i];
                    if (!IsValid(blocker) || prompt.yMax <= blocker.yMin - padding || prompt.yMin >= blocker.yMax + padding)
                        continue;
                    float blockLeft = Mathf.Max(left, blocker.xMin - padding);
                    float blockRight = Mathf.Min(right, blocker.xMax + padding);
                    if (blockRight <= blockLeft) continue;
                    for (int j = scratch.Count - 1; j >= 0; j--)
                    {
                        var interval = scratch[j];
                        if (blockRight <= interval.x || blockLeft >= interval.y) continue;
                        if (blockLeft <= interval.x && blockRight >= interval.y)
                            scratch.RemoveAt(j);
                        else if (blockLeft <= interval.x)
                            scratch[j] = new Vector2(blockRight, interval.y);
                        else if (blockRight >= interval.y)
                            scratch[j] = new Vector2(interval.x, blockLeft);
                        else
                        {
                            scratch[j] = new Vector2(interval.x, blockLeft);
                            scratch.Insert(j + 1, new Vector2(blockRight, interval.y));
                        }
                    }
                }
            }

            float originalCenter = prompt.center.x;
            float bestCenter = originalCenter;
            float bestDistance = float.PositiveInfinity;
            bool fullSize = false;
            for (int i = 0; i < scratch.Count; i++)
            {
                var interval = scratch[i];
                if (interval.y - interval.x < prompt.width) continue;
                float center = Mathf.Clamp(originalCenter, interval.x + prompt.width * 0.5f, interval.y - prompt.width * 0.5f);
                float distance = Mathf.Abs(center - originalCenter);
                if (distance >= bestDistance) continue;
                fullSize = true;
                bestDistance = distance;
                bestCenter = center;
            }
            if (fullSize)
            {
                fitted = new Rect(bestCenter - prompt.width * 0.5f, prompt.y, prompt.width, prompt.height);
                return true;
            }

            float bestWidth = 0f;
            bestDistance = float.PositiveInfinity;
            for (int i = 0; i < scratch.Count; i++)
            {
                var interval = scratch[i];
                float width = interval.y - interval.x;
                float center = (interval.x + interval.y) * 0.5f;
                float distance = Mathf.Abs(center - originalCenter);
                if (width < bestWidth || (width == bestWidth && distance >= bestDistance)) continue;
                bestWidth = width;
                bestCenter = center;
                bestDistance = distance;
            }
            if (bestWidth <= 0f) return false;
            scale = Mathf.Min(1f, bestWidth / prompt.width);
            float height = prompt.height * scale;
            fitted = new Rect(bestCenter - bestWidth * 0.5f, prompt.center.y - height * 0.5f, bestWidth, height);
            return true;
        }

        private static bool IsValid(Rect rectangle)
        {
            return IsFinite(rectangle.xMin) && IsFinite(rectangle.xMax) && IsFinite(rectangle.yMin) && IsFinite(rectangle.yMax)
                && rectangle.width > 0f && rectangle.height > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
