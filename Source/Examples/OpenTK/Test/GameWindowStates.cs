// This code was written for the OpenTK library and has been released
// to the Public Domain.
// It is provided "as is" without express or implied warranty of any kind.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

#pragma warning disable 612,618 // disable obsolete warnings - we need to access these functions

namespace Examples.Tests
{
    [Example("GameWindow States", ExampleCategory.OpenTK, "GameWindow", 4, Documentation = "GameWindowStates")]
    public class GameWindowStates : GameWindow
    {
        static readonly Font TextFont = new Font(FontFamily.GenericSansSerif, 11);
        Bitmap TextBitmap = new Bitmap(1024, 1024);
        StringBuilder TypedText = new StringBuilder();
        int texture;
        bool mouse_in_window = false;
        bool viewport_changed = true;

        MouseCursor Pencil;

        // new GameWindow.Mouse* events
        Vector4 mouse_pos;
        int mouse_buttons;
        MouseState mouse_state;

        //new GameWindow.Key* events
        Dictionary<Key, int> keyboard_keys = new Dictionary<Key, int>();
        KeyboardState keyboard_state;
        KeyModifiers keyboard_modifiers;

        // time drift
        Stopwatch watch = new Stopwatch();
        double update_time, render_time;

        // timing information
        double timestamp;
        int update_count;
        int update_fps;
        int render_count;
        int render_fps;

        // position of moving objects on screen
        double variable_update_timestep_pos = -1;
        double variable_refresh_timestep_pos = -1;
        double fixed_update_timestep_pos = -1;

        public GameWindowStates()
            : base(800, 600, GraphicsMode.Default)
        {
            VSync = VSyncMode.On;
            KeyDown += KeyDownHandler;
            KeyUp += KeyUpHandler;
            KeyPress += KeyPressHandler;

            MouseEnter += delegate { mouse_in_window = true; };
            MouseLeave += delegate { mouse_in_window = false; };

            MouseMove += MouseMoveHandler;
            MouseWheel += MouseWheelHandler;
            MouseDown += MouseButtonHandler;
            MouseUp += MouseButtonHandler;
        }

        private void KeyPressHandler(object sender, KeyPressEventArgs e)
        {
            if (TypedText.Length > 32)
                TypedText.Remove(0, 1);

            TypedText.Append(e.KeyChar);
        }
        private void KeyDownHandler(object sender, KeyboardKeyEventArgs e)
        {
            switch (e.Key)
            {
                case OpenTK.Input.Key.Escape:
                    if (!CursorVisible)
                        CursorVisible = true;
                    else
                        Exit();
                    break;

                case Key.Number1: WindowState = WindowState.Normal; break;
                case Key.Number2: WindowState = WindowState.Maximized; break;
                case Key.Number3: WindowState = WindowState.Fullscreen; break;
                case Key.Number4: WindowState = WindowState.Minimized; break;

                case Key.Number5: WindowBorder = WindowBorder.Resizable; break;
                case Key.Number6: WindowBorder = WindowBorder.Fixed; break;
                case Key.Number7: WindowBorder = WindowBorder.Hidden; break;

                case Key.Left: X = X - 16; break;
                case Key.Right: X = X + 16; break;
                case Key.Up: Y = Y - 16; break;
                case Key.Down: Y = Y + 16; break;

                case Key.KeypadPlus:
                case Key.Plus: Size += new Size(16, 16); break;

                case Key.KeypadMinus:
                case Key.Minus: Size -= new Size(16, 16); break;

                case Key.V:
                    VSync = VSync == VSyncMode.On ? VSyncMode.Off : VSyncMode.On;
                    break;

                case Key.BracketLeft: TargetUpdateFrequency--; break;
                case Key.BracketRight: TargetUpdateFrequency++; break;
                case Key.Comma: TargetRenderFrequency--; break;
                case Key.Period: TargetRenderFrequency++; break;

                case Key.Enter:
                    CursorVisible = !CursorVisible;
                    break;

                case Key.C:
                    if (Cursor == MouseCursor.Default)
                        Cursor = Pencil;
                    else if (Cursor == Pencil)
                        Cursor = MouseCursor.Empty;
                    else
                        Cursor = MouseCursor.Default;
                    break;

                case Key.Space:
                    Point p = new Point(Width / 2, Height / 2);
                    p = PointToScreen(p);
                    OpenTK.Input.Mouse.SetPosition(p.X, p.Y);
                    break;
            }

            if (!keyboard_keys.ContainsKey(e.Key))
            {
                keyboard_keys.Add(e.Key, 0);
            }
            keyboard_keys[e.Key] = keyboard_keys[e.Key] + 1;
            keyboard_modifiers = e.Modifiers;
            keyboard_state = e.Keyboard;
        }
        private void KeyUpHandler(object sender, KeyboardKeyEventArgs e)
        {
            keyboard_keys.Remove(e.Key);
            keyboard_modifiers = e.Modifiers;
            keyboard_state = e.Keyboard;
        }

        private void MouseMoveHandler(object sender, MouseMoveEventArgs e)
        {
            mouse_pos.X = e.X;
            mouse_pos.Y = e.Y;
            mouse_pos.Z = e.Mouse.Scroll.X;
            mouse_pos.W = e.Mouse.Scroll.Y;
            mouse_state = e.Mouse;
        }
        private void MouseButtonHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.IsPressed)
            {
                mouse_buttons |= 1 << (int)e.Button;
            }
            else
            {
                mouse_buttons &= ~(1 << (int)e.Button);
            }
            mouse_state = e.Mouse;
        }
        private void MouseWheelHandler(object sender, MouseWheelEventArgs e)
        {
            mouse_pos.Z = e.Mouse.Scroll.X;
            mouse_pos.W = e.Mouse.Scroll.Y;
            mouse_state = e.Mouse;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            double clock_time = watch.Elapsed.TotalSeconds;
            update_time += e.Time;
            timestamp += e.Time;
            update_count++;

            using (Graphics gfx = Graphics.FromImage(TextBitmap))
            {
                int line = 0;

                gfx.Clear(Color.Black);
                gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // OpenGL information
                DrawString(gfx, GL.GetString(StringName.Renderer), line++);
                DrawString(gfx, GL.GetString(StringName.Version), line++);
                DrawString(gfx, Context.GraphicsMode.ToString(), line++);

                // GameWindow information
                line++;
                DrawString(gfx, "GameWindow:", line++);
                DrawString(gfx, String.Format("[1 - 4]:[5 - 7]: WindowState.{0}:WindowBorder.{1}",
                    this.WindowState, this.WindowBorder), line++);
                DrawString(gfx, String.Format("[V]: VSync.{0}.", VSync), line++);
                DrawString(gfx, String.Format("Bounds: {0}", Bounds), line++);
                DrawString(gfx, String.Format("ClientRectangle: {0}", ClientRectangle), line++);
                DrawString(gfx, String.Format("Mouse {0} and {1}. {2}.",
                    mouse_in_window ? "inside" : "outside",
                    CursorVisible ? "visible" : "hidden",
                    Focused ? "Focused" : "Not focused"), line++);
                DrawString(gfx, String.Format("Mouse State: {0}",
                    mouse_state), line++);
                DrawString(gfx, String.Format("Text: {0}", TypedText.ToString()), line++);

                // Timing information
                line++;
                DrawString(gfx, "Timing:", line++);
                DrawString(gfx,
                    String.Format("Frequency: update {4} ({0:f2}/{1:f2}); render {5} ({2:f2}/{3:f2})",
                        UpdateFrequency, TargetUpdateFrequency,
                        RenderFrequency, TargetRenderFrequency,
                        update_fps, render_fps),
                    line++);
                DrawString(gfx,
                    String.Format("Period: update {4:N4} ({0:f4}/{1:f4}); render {5:N4} ({2:f4}/{3:f4})",
                        UpdatePeriod, TargetUpdatePeriod,
                        RenderPeriod, TargetRenderPeriod,
                        1.0 / update_fps, 1.0 / render_fps),
                    line++);
                DrawString(gfx, String.Format("Time: update {0:f4}; render {1:f4}",
                    UpdateTime, RenderTime), line++);
                DrawString(gfx, String.Format("Drift: clock {0:f4}; update {1:f4}; render {2:f4}",
                    clock_time, clock_time - update_time, clock_time - render_time), line++);

                if (timestamp >= 1)
                {
                    timestamp -= 1;
                    update_fps = update_count;
                    render_fps = render_count;
                    update_count = 0;
                    render_count = 0;

                }

                // Input information
                line = DrawKeyboards(gfx, line);
                line = DrawMice(gfx, line);
                line = DrawJoysticks(gfx, line);
            }

            fixed_update_timestep_pos += TargetUpdatePeriod;
            variable_update_timestep_pos += e.Time;
            if (fixed_update_timestep_pos >= 1)
                fixed_update_timestep_pos -= 2;
            if (variable_update_timestep_pos >= 1)
                variable_update_timestep_pos -= 2;
        }
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            render_time += e.Time;
            render_count++;

            GL.Clear(ClearBufferMask.ColorBufferBit);

            if (viewport_changed)
            {
                viewport_changed = false;
                GL.Viewport(0, 0, Width, Height);
            }

            DrawText();

            DrawMovingObjects();

            variable_refresh_timestep_pos += e.Time;
            if (variable_refresh_timestep_pos >= 1)
                variable_refresh_timestep_pos -= 2;

            SwapBuffers();
        }

        protected override void OnLoad(EventArgs e)
        {
            watch.Start();

            using (var bitmap = new Bitmap("Data/Textures/cursor.png"))
            {
                var data = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height), 
                    System.Drawing.Imaging.ImageLockMode.ReadOnly, 
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

                Pencil = new OpenTK.MouseCursor(
                    2, 21, data.Width, data.Height, data.Scan0);
            }

            GL.ClearColor(Color.MidnightBlue);

            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcColor);

            texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, TextBitmap.Width, TextBitmap.Height,
                0, PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Nearest);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            viewport_changed = true;
        }

        // Uploads our text Bitmap to an OpenGL texture
        // and displays is to screen.
        private void DrawText()
        {
            System.Drawing.Imaging.BitmapData data = TextBitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, TextBitmap.Width, TextBitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, TextBitmap.Width, TextBitmap.Height, PixelFormat.Bgra,
                PixelType.UnsignedByte, data.Scan0);
            TextBitmap.UnlockBits(data);

            Matrix4 text_projection = Matrix4.CreateOrthographicOffCenter(0, Width, Height, 0, -1, 1);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref text_projection);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            GL.Color4(Color4.White);
            GL.Enable(EnableCap.Texture2D);
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0, 0); GL.Vertex2(0, 0);
            GL.TexCoord2(1, 0); GL.Vertex2(TextBitmap.Width, 0);
            GL.TexCoord2(1, 1); GL.Vertex2(TextBitmap.Width, TextBitmap.Height);
            GL.TexCoord2(0, 1); GL.Vertex2(0, TextBitmap.Height);
            GL.End();
            GL.Disable(EnableCap.Texture2D);
        }
        private void DrawRectangle()
        {
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex2(-0.05, -0.05);
            GL.Vertex2(+0.05, -0.05);
            GL.Vertex2(+0.05, +0.05);
            GL.Vertex2(-0.05, +0.05);
            GL.End();
        }

        // Draws three moving objects, using three different timing methods:
        // 1. fixed framerate based on TargetUpdatePeriod
        // 2. variable framerate based on UpdateFrame e.Time
        // 3. variable framerate based on RenderFrame e.Time
        // If the timing implementation is correct, all three objects
        // should be moving at the same speed, regardless of the current
        // UpdatePeriod and RenderPeriod.
        private void DrawMovingObjects()
        {
            Matrix4 thing_projection = Matrix4.CreateOrthographic(2, 2, -1, 1);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref thing_projection);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.Translate(fixed_update_timestep_pos, -0.2, 0);
            GL.Color4(Color4.Red);
            DrawRectangle();

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.Translate(variable_update_timestep_pos, -0.4, 0);
            GL.Color4(Color4.DarkGoldenrod);
            DrawRectangle();

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.Translate(variable_refresh_timestep_pos, -0.8, 0);
            GL.Color4(Color4.DarkGreen);
            DrawRectangle();
        }


        private static int Clamp(int val, int min, int max)
        {
            return val > max ? max : val < min ? min : val;
        }

        private static float DrawString(Graphics gfx, string str, int line)
        {
            return DrawString(gfx, str, line, 0);
        }
        private static float DrawString(Graphics gfx, string str, int line, float offset)
        {
            gfx.DrawString(str, TextFont, Brushes.White, new PointF(offset, line * TextFont.Height));
            return offset + gfx.MeasureString(str, TextFont).Width;
        }

        private static void KeyboardStateToString(KeyboardState state, StringBuilder sb)
        {
            for (int key_index = 0; key_index < (int)Key.LastKey; key_index++)
            {
                Key k = (Key)key_index;
                if (state[k])
                {
                    sb.Append(k);
                    sb.Append(" ");
                }
            }
        }

        private static int DrawKeyboards(Graphics gfx, int line)
        {
            line++;
            DrawString(gfx, "Keyboard:", line++);
            for (int i = 0; i < 4; i++)
            {
                var state = OpenTK.Input.Keyboard.GetState(i);
                if (state.IsConnected)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(i);
                    sb.Append(": ");
                    KeyboardStateToString(state, sb);
                    DrawString(gfx, sb.ToString(), line++);
                }
            }
            return line;
        }
        private static int DrawMice(Graphics gfx, int line)
        {
            line++;
            MouseState cursorState = Mouse.GetCursorState();
            DrawString(gfx, String.Format("Cursor: {0}", cursorState), line++);
            DrawString(gfx, "Mouse:", line++);
            for (int i = 0; i < 4; i++)
            {
                var state = OpenTK.Input.Mouse.GetState(i);
                if (state.IsConnected)
                {
                    DrawString(gfx, state.ToString(), line++);
                }
            }
            return line;
        }
        private static int DrawJoysticks(Graphics gfx, int line)
        {
            line++;
            DrawString(gfx, "GamePad:", line++);
            for (int i = 0; i < 4; i++)
            {
                GamePadCapabilities caps = GamePad.GetCapabilities(i);
                GamePadState state = GamePad.GetState(i);
                if (state.IsConnected)
                {
                    DrawString(gfx, String.Format("[{0}] {1} ({2})", i, Joystick.GetGuid(i), GamePad.GetName(i)), line++);
                    DrawString(gfx, String.Format("{0}", caps), line++);
                    DrawString(gfx, state.ToString(), line++);
                }
            }

            line++;
            DrawString(gfx, "Joystick:", line++);
            for (int i = 0; i < 4; i++)
            {
                JoystickCapabilities caps = Joystick.GetCapabilities(i);
                JoystickState state = Joystick.GetState(i);
                if (state.IsConnected)
                {
                    DrawString(gfx, String.Format("[{0}] {1}", i, Joystick.GetGuid(i)), line++);
                    DrawString(gfx, String.Format("{0}", caps), line++);
                    DrawString(gfx, state.ToString(), line++);
                }
            }

            return line;
        }

        public static void Main()
        {
            using (GameWindowStates ex = new GameWindowStates())
            {
                Utilities.SetWindowTitle(ex);
                ex.Run(30.0);
            }
        }
    }
}
