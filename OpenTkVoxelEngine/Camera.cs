using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static System.Net.Mime.MediaTypeNames;

namespace OpenTkVoxelEngine
{
    class Camera
    {
        //Reference to the active window
        GameWindow _window;

        //Camera Matrices
        Matrix4 _projection = Matrix4.Identity;
        public Matrix4 Projection() => _projection;

        Matrix4 _model = Matrix4.Identity;
        public Matrix4 Model() => _model;
         
        Matrix4 _view = Matrix4.Identity;
        public Matrix4 View() => _view;


        


        //Camera Variables
        float _FOV = 45.0f;
        public float FOV() => _FOV;

        float _maxFOV = 45.0f;
        float _nearPlane = .1f;
        float _farPlane = 100f;
        float _speed = 12f;
        float _sensitivity = 1.0f;

        Vector3 _position = Vector3.Zero;
        Vector3 _front = Vector3.UnitZ;
        Vector3 _up = Vector3.UnitY;
        Vector3 _right = Vector3.UnitX;

        public Vector3 Up() => _up;
        public Vector3 Forward() => _front;
        public Vector3 Right() => _right;

        Vector2 lastPos;
            
        float _yaw = 90;
        float _pitch = 0;

        bool _cursorLocked = true;
        bool _firstMove = true;

        public Camera(GameWindow window, float nearPlane, float farPlane)
        {
            _window = window;
            _nearPlane = nearPlane;
            _farPlane = farPlane;


            //Create default matrix
            _model = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-55.0f));
            _view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f);
            _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(_FOV), (float)_window.Size.X / (float)_window.Size.Y, _nearPlane, _farPlane);


            //Lock the cusror
            _window.CursorState = CursorState.Grabbed;


        }

        public Vector3 Position() => _position;

        public void OnUpdateFrame(FrameEventArgs args)
        {
            ListenForInput(args);


            Look(args);
        }

        void Look(FrameEventArgs args)
        {
            //Stop mouse input when locked
            if(!_cursorLocked) return;  


            var mouse = _window.MouseState;
            if (_firstMove)
            {
                lastPos = new Vector2(mouse.X, mouse.Y);
                _firstMove = false;
                return;
            }
            //Capture the mouse delta each frame

            var deltaX = mouse.X - lastPos.X;
            var deltaY = mouse.Y - lastPos.Y;

            lastPos = new Vector2(mouse.X, mouse.Y);


            //Clamp Pitch
            if (_pitch + deltaY > 89.0f)
            {
                _pitch = 88.0f;
            }else if (_pitch - deltaY < -89.0f)
            {
                _pitch = -88.0f;
            }
            else
            { 
                _pitch -= deltaY * _sensitivity;
            }
            _yaw += deltaX * _sensitivity;

            _front.X = (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(_yaw));
            _front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(_pitch));
            _front.Z = (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(_yaw));

            _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY)).Normalized();

            _up = Vector3.Cross(_right, _front).Normalized();

            _view = GetViewModel();

        }


        public Matrix4 GetViewModel()
        {
            return Matrix4.LookAt(_position, _position + _front, _up);
        }

        void ListenForInput(FrameEventArgs args)
        {

            //Handles the movement
            if (_window.IsKeyDown(Keys.W))
            {
                _position += _front * _speed * (float)args.Time; //Forward 
            }

            if (_window.IsKeyDown(Keys.S))
            {
                _position -= _front * _speed * (float)args.Time; //Backwards
            }

            if (_window.IsKeyDown(Keys.A))
            {
                _position -= Vector3.Normalize(Vector3.Cross(_front, _up)) * _speed * (float)args.Time; //Left
            }

            if (_window.IsKeyDown(Keys.D))
            {
                _position += Vector3.Normalize(Vector3.Cross(_front, _up)) * _speed * (float)args.Time; //Right
            }

            if (_window.IsKeyDown(Keys.Space))
            {
                _position += _up * _speed * (float)args.Time; //Up 
            }

            if (_window.IsKeyDown(Keys.LeftShift))
            {
                _position -= _up * _speed * (float)args.Time; //Down
            }

            //Handle closing the window and repositions mouse
            if (_window.IsKeyDown(Keys.LeftAlt) && _cursorLocked == true)
            {
                _cursorLocked = false;
            }
            else if (_cursorLocked == false && !_window.IsKeyDown(Keys.LeftAlt))
            {
                _cursorLocked = true;
                _firstMove = true;
            }


        }

        //Handles the zooming
        public void OnMouseWheel(MouseWheelEventArgs e)
        {
            _FOV = Math.Clamp(_FOV - e.OffsetY,1,_maxFOV);
            

            _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(_FOV), (float)_window.Size.X / (float)_window.Size.Y, _nearPlane, _farPlane);


        }


        public void OnUnload()
        {

        }





    }
}
