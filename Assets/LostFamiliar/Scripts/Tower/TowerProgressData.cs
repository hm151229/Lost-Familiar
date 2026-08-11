using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostFamiliar.Core
{
    public enum TowerType { Gold, Gem }
    public enum TowerGrade { F, C, B, A, S }

    [Serializable]
    public sealed class TowerProgressData
    {
        public int tickets = 2;
        public int highestUnlockedFloor = 1;
        public string lastDailyTicketDate = string.Empty;
        public List<int> bestGrades = new List<int>();
        public List<float> bestClearTimes = new List<float>();

        public void Normalize()
        {
            tickets = Math.Max(0, tickets);
            highestUnlockedFloor = Math.Max(1, highestUnlockedFloor);
            lastDailyTicketDate ??= string.Empty;
            bestGrades ??= new List<int>();
            bestClearTimes ??= new List<float>();
            for (int i = 0; i < bestGrades.Count; i++)
                bestGrades[i] = Mathf.Clamp(bestGrades[i], (int)TowerGrade.F, (int)TowerGrade.S);
        }

        public bool RefreshDailyTickets(string today)
        {
            Normalize();
            if (lastDailyTicketDate == today)
                return false;
            tickets = Math.Max(tickets, TowerBalance.DailyTickets);
            lastDailyTicketDate = today;
            return true;
        }

        public TowerGrade GetBestGrade(int floor)
        {
            int index = Math.Max(1, floor) - 1;
            return index < bestGrades.Count
                ? (TowerGrade)Mathf.Clamp(bestGrades[index], 0, (int)TowerGrade.S)
                : TowerGrade.F;
        }

        public void RecordClear(int floor, TowerGrade grade)
        {
            RecordClear(floor, grade, -1f);
        }

        public void RecordClear(int floor, TowerGrade grade, float clearTime)
        {
            floor = Math.Max(1, floor);
            while (bestGrades.Count < floor)
                bestGrades.Add((int)TowerGrade.F);
            bestGrades[floor - 1] = Math.Max(bestGrades[floor - 1], (int)grade);
            while (bestClearTimes.Count < floor) bestClearTimes.Add(-1f);
            if (clearTime >= 0f && (bestClearTimes[floor - 1] < 0f || clearTime < bestClearTimes[floor - 1]))
                bestClearTimes[floor - 1] = clearTime;
            highestUnlockedFloor = Math.Max(highestUnlockedFloor, floor + 1);
        }

        public float GetBestClearTime(int floor)
        {
            int index = Math.Max(1, floor) - 1;
            return index < bestClearTimes.Count ? bestClearTimes[index] : -1f;
        }
    }
}
