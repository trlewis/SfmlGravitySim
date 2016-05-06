﻿namespace SfmlGravityWpf.GameControllers
{
    using System.Collections.Generic;
    using System.Linq;

    using GameModels;
    using SFML.System;
    using SFML.Graphics;

    using GameCode;

    public class GravityObjectController
    {
        private readonly Clock _timer = new Clock();
        private const float LineMass = 10;
        private bool _addingShape;
        private Vector2f _startPoint; //new shape's spawn point, start of initial vector line
        private Vector2f _endPoint; //used to calculate new shape's initial velocity, end of init velocity line

        public GravityObjectController()
        {
            this.IsRunning = true;
            this.GravityObjects = new List<GravityObject>();
        }

        public void StartNewShape(Vector2f startPoint)
        {
            if (this._addingShape)
                return;
            this._addingShape = true;
            this._startPoint = startPoint;
            this._endPoint = startPoint; //so line is 0 length at start
        }

        public void UpdateEndPoint(Vector2f endPoint)
        {
            if (!this._addingShape)
                return;
            this._endPoint = endPoint;
        }

        public void FinishAddingShape(float mass, float radius, GravityObjectType type)
        {
            var vel = this._endPoint - this._startPoint;

            GravityObject gs = null;
            switch (type)
            {
                case GravityObjectType.Asteroid:
                    gs = new GravityAsteroid(this._startPoint, mass) {Velocity = vel};
                    break;
                case GravityObjectType.Circle:
                    var circle = new CircleShape(radius) { FillColor = Color.Cyan, Position = this._startPoint};
                    gs = new GravityPoint(circle, mass) {Velocity = vel};
                    break;
            }

            if(gs != null)
                this.GravityObjects.Add(gs);

            this._addingShape = false;
        }

        public bool DrawGravityField { get; set; }

        public bool DrawGravityFieldAsLines { get; set; }

        public bool DrawGravityFieldAsGradient { get; set; }

        public bool DrawMotionTrails { get; set; }

        public bool DrawVelocityLines { get; set; }

        private IList<GravityObject> GravityObjects { get; set; }

        public bool IsRunning { get; private set; }

        public int ShapeCount
        {
            get { return this.GravityObjects.Count; }
        }

        public void DeleteShapes()
        {
            this.GravityObjects.Clear();
        }

        public void Draw(RenderTarget target)
        {
            if (this.DrawGravityField)
                this.DrawForceField(target);

            var drawables = this.GravityObjects.Where(g => g is GravityDrawable).Select(g => (GravityDrawable)g);

            if (this.DrawMotionTrails)
            {
                foreach (var gd in drawables)
                    gd.MotionTrail.Draw(target);              
            }

            foreach (var gd in drawables)
                gd.Draw(target);

            if (this.DrawVelocityLines)
                this.DrawVelocity(target);

            if (this._addingShape)
            {
                var startVertex = new Vertex(this._startPoint, Color.Magenta);
                var endVertex = new Vertex(this._endPoint, Color.Green);
                var line = new[] { startVertex, endVertex };
                target.Draw(line, PrimitiveType.Lines);
            }
        }

        private void DrawForceField(RenderTarget target)
        {
            if (this.DrawGravityFieldAsLines)
            {
                const int spacing = 20;
                for (int y = 0; y <= target.Size.Y / spacing; y++)
                {
                    for (int x = 0; x <= target.Size.X / spacing; x++)
                    {
                        var start = new Vector2f(x * spacing, y * spacing);
                        var line = new ForceLine(start, LineMass, spacing);
                        line.CalculateLine(this.GravityObjects);
                        target.Draw(line.Line, PrimitiveType.Lines);
                    }
                }
            }

            if (this.DrawGravityFieldAsGradient)
            {
                const int spacing = 10;
                int width = (int)target.Size.X / spacing;
                int height = (int)target.Size.Y / spacing;
                var field = new GravityGradientField(spacing, width, height);
                field.Calculate(this.GravityObjects);
                field.Draw(target);
            }
        }

        private void DrawVelocity(RenderTarget target)
        {
            var purple = new Color(153, 0, 255);
            foreach (var gs in this.GravityObjects)
            {
                var start = new Vertex(gs.GlobalCenterOfMass, Color.White);
                var end = new Vertex(gs.GlobalCenterOfMass + gs.Velocity, purple);
                var line = new[] { start, end };
                target.Draw(line, PrimitiveType.Lines);
            }
        }
 
        public void Pause()
        {
            this.IsRunning = false;
        }

        public void Run()
        {
            this.IsRunning = true;
        }

        /// <summary>
        /// Updates the position/velocity of the GravityShapes since the last time Update() was called.
        /// </summary>
        public void Update()
        {
            //for now we're just going to run it at "60 FPS". It really should be frame independend though.
            const float dSeconds = 1f/60f;
            
            if (!this.IsRunning)
                return;

            foreach (var go in this.GravityObjects)
                go.Move(dSeconds);

            this.CheckCollisions();

            foreach (var go in this.GravityObjects)
            {
                go.CalculateForce(this.GravityObjects);
                go.UpdateVelocity(dSeconds);
            }
        }

        private void CheckCollisions()
        {
            for (int i = 0; i < this.GravityObjects.Count; i++)
            {
                var outerCollidable = this.GravityObjects[i] as ICollidableGravityObject;
                if (outerCollidable == null)
                    continue;

                for (int j = i + 1; j < this.GravityObjects.Count; j++)
                {
                    var innerCollidable = this.GravityObjects[j] as ICollidableGravityObject;
                    if (innerCollidable == null)
                        continue;

                    var checker = new CollisionChecker(outerCollidable, innerCollidable);
                    if (!checker.AreColliding())
                        continue;

                    outerCollidable.HandleCollision(checker.CollisionPoint);
                    innerCollidable.HandleCollision(checker.CollisionPoint);
                }
            }
        }
    }
}
