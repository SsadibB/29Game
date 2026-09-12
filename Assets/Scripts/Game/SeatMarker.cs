using UnityEngine;

namespace Game29
{
    /// <summary>
    /// Marks a scene GameObject as the anchor point for a specific player seat.
    /// Used by the UI layer to know where to place cards, hand displays, etc.
    ///
    /// Also draws a labelled gizmo in the Scene view so you can see the table
    /// layout at a glance without entering Play mode.
    /// </summary>
    public class SeatMarker : MonoBehaviour
    {
        [Tooltip("Which player seat this GameObject represents.")]
        public PlayerSeat Seat;

        // ── Editor gizmos ────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color col = GetSeatColour();
            Gizmos.color = col;

            // Draw a disc representing the seat position.
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // Label
            UnityEditor.Handles.color = col;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                Seat == PlayerSeat.South ? $"{Seat} ★" : Seat.ToString()
            );
        }

        private Color GetSeatColour()
        {
            switch (Seat)
            {
                case PlayerSeat.South: return Color.cyan;    // Human — stands out
                case PlayerSeat.North: return Color.green;   // Partner
                case PlayerSeat.East:  return Color.red;     // Opponent
                case PlayerSeat.West:  return Color.red;     // Opponent
                default:               return Color.white;
            }
        }
#endif

        // ── Runtime helper ───────────────────────────────────────────────────

        /// <summary>
        /// Find the SeatMarker in the scene for a given seat (slow — only for init).
        /// </summary>
        public static SeatMarker Find(PlayerSeat seat)
        {
            foreach (var marker in FindObjectsByType<SeatMarker>(FindObjectsSortMode.None))
                if (marker.Seat == seat) return marker;
            return null;
        }
    }
}
