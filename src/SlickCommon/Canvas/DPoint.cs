﻿namespace SlickCommon.Canvas
{
    /// <summary>
    /// Double precision point with pressure
    /// </summary>
    public struct DPoint
    {
        public double X;
        public double Y;
        public double Pressure;
        public bool IsErase;
        public int StylusId;
    }
}