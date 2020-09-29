﻿using AAEmu.DBDefs;
using FreeImageAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Management.Instrumentation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AAEmu.DBViewer
{
    public partial class MapViewForm : Form
    {
        public static MapViewForm ThisForm ;
        private PictureBox pb = new PictureBox();
        private Point viewOffset = new Point(0, 0);
        private Point startDragPos = new Point();
        private Point startDragOffset = new Point();
        private bool isDragging = false;
        private float viewScale = 1f;
        private MapLevel minimumDrawLevel = MapLevel.None;
        private List<MapViewPoI> poi = new List<MapViewPoI>();
        private List<MapViewMap> subMaps = new List<MapViewMap>();
        private RectangleF FocusBorder = new RectangleF();

        public Point ViewOffset { get => viewOffset; set { viewOffset = value; updateStatusBar(); } }

        public MapViewForm()
        {
            MapViewForm.ThisForm = this;
            InitializeComponent();

            pb.SetBounds(0, 0, 35536, 35536);

            pView.MouseWheel += new MouseEventHandler(MapViewOnMouseWheel);
            viewOffset.Y = 20000;
            viewScale = 0.05f;

            ClearPoI();
            ClearMaps();
            MapViewImageHelper.PopulateList();

            AddMap(-14731,46,64651,38865,"Erenor","game/ui/map/world/", "main_world",MapLevel.WorldMap);

            foreach(var zgv in AADB.DB_Zone_Groups)
            {
                var zg = zgv.Value;
                var iref = MapViewImageHelper.GetRefByImageMapId(zg.image_map);
                var fn = string.Empty;
                if (iref == null)
                    fn = zg.name;
                else
                    fn = iref.FileName;

                //var m = AddMap(zg.PosAndSize.X, zg.PosAndSize.Y, zg.PosAndSize.Width, zg.PosAndSize.Height, zg.display_textLocalized + " ("+zg.id.ToString()+")", "game/ui/map/world/", fn, MapLevel.Zone);
                var m = AddMap(zg.PosAndSize.X, zg.PosAndSize.Y, zg.PosAndSize.Width, zg.PosAndSize.Height, zg.display_textLocalized, "game/ui/map/road/", zg.name + "_road_100", MapLevel.Zone);

                if (zg.faction_chat_region_id == 2)
                    m.MapBorderColor = Color.DarkCyan;
                if (zg.faction_chat_region_id == 3)
                    m.MapBorderColor = Color.DarkGreen;
                if (zg.faction_chat_region_id == 4)
                    m.MapBorderColor = Color.DarkRed;
                // If it has the Rough Sea (Turbulance) buff, it's the ocean, use a blue border
                if (zg.buff_id == 7743)
                    m.MapBorderColor = Color.Navy;
            }

        }

        public static MapViewForm GetMap()
        {
            if (ThisForm != null)
                return ThisForm;
            else
                return new MapViewForm();
        }

        private void updateStatusBar()
        {
            var s = string.Empty;
            s += ViewOffset.X.ToString() + " , " + ViewOffset.Y.ToString() + " | " + (viewScale * 100).ToString()+"%" ;
            if (isDragging)
                s += " | drag: " + startDragPos.X.ToString() + " , " + startDragPos.Y.ToString();
            tsslPos.Text = s;
        }

        private void MapViewOnMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (viewScale >= 2f)
                    viewScale += 0.25f;
                else
                    viewScale += 0.025f;
            }

            if (e.Delta < 0)
            {
                if (viewScale >= 2f)
                    viewScale -= 0.25f;
                else
                    viewScale -= 0.025f;
            }

            if (viewScale < 0.025f)
                viewScale = 0.025f;
            pView.Invalidate();
            updateStatusBar();
        }

        private Point CoordToPixel(Point coord) => CoordToPixel(coord.X,coord.Y);

        private Point CoordToPixel(float x, float y)
        {
            return new Point((int)x, (int)y * -1);
        }

        private void DrawCross(Graphics g,float x, float y, Color color, string name)
        {
            int crossSize = (int)(6f / viewScale) + 1 ;
            var pen = new Pen(color);
            var pos = CoordToPixel(x, y);
            g.DrawLine(pen, ViewOffset.X + pos.X, ViewOffset.Y + pos.Y - crossSize, ViewOffset.X + x, ViewOffset.Y + pos.Y + crossSize);
            g.DrawLine(pen, ViewOffset.X + pos.X - crossSize, ViewOffset.Y + pos.Y, ViewOffset.X + pos.X + crossSize, ViewOffset.Y + pos.Y);
            if ((cbPoINames.Checked) && (name != string.Empty) )
            {
                var f = new Font(Font.FontFamily, 16f / viewScale);
                var br = new SolidBrush(color);
                g.DrawString(name, f, br, ViewOffset.X + pos.X + crossSize, ViewOffset.Y + pos.Y - crossSize);
            }
        }

        private void DrawSubMap(Graphics g, MapViewMap map)
        {
            var pen = new Pen(map.MapBorderColor);
            var pos = CoordToPixel(map.ZoneCoordX, map.ZoneCoordY);
            var imgPos = CoordToPixel(map.ImgCoordX, map.ImgCoordY);

            var targetRect = new RectangleF(ViewOffset.X + pos.X, ViewOffset.Y + pos.Y - map.ZoneHeight, map.ZoneWidth, map.ZoneHeight);
            if (cbZoneBorders.Checked)
                g.DrawRectangle(pen, targetRect.X, targetRect.Y, targetRect.Width, targetRect.Height);

            if (map.BitmapImage != null)
            {
                float targetAspect = targetRect.Width / targetRect.Height;
                float imageAspect = (float)map.BitmapImage.Width / (float)map.BitmapImage.Height;
                var drawRect = targetRect ;

                if (targetAspect >= imageAspect)
                {
                    drawRect = new RectangleF();
                    drawRect.Width = targetRect.Width / targetAspect * imageAspect;
                    drawRect.Height = targetRect.Height;
                    drawRect.X = targetRect.X + ((targetRect.Width - drawRect.Width) / 2);
                    drawRect.Y = targetRect.Y ;
                }
                else
                {
                    drawRect = new RectangleF();
                    drawRect.Width = targetRect.Width;
                    drawRect.Height = targetRect.Height * targetAspect / imageAspect;
                    drawRect.X = targetRect.X;
                    drawRect.Y = targetRect.Y + ((targetRect.Height - drawRect.Height) / 2);
                }

                g.DrawImage(map.BitmapImage, drawRect);
                if (cbZoneBorders.Checked)
                    g.DrawRectangle(Pens.Silver, drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height);
            }
            //g.DrawImage(map.BitmapImage, ViewOffset.X + imgPos.X, ViewOffset.Y + imgPos.Y - map.ImgHeight, map.ImgWidth, map.ImgHeight);

            if (cbZoneBorders.Checked && (map.Name != string.Empty))
            {
                var f = new Font(Font.FontFamily, 100f);
                var br = new SolidBrush(map.MapBorderColor);
                g.DrawString(map.Name, f, br, ViewOffset.X + pos.X, ViewOffset.Y + pos.Y);
            }
        }

        private void pView_Paint(object sender, PaintEventArgs e)
        {
            // Draw the map, or something like that

            // Create a local version of the graphics object for the PictureBox.
            Graphics g = e.Graphics;

            g.ScaleTransform(viewScale, viewScale);

            // Draw a string on the PictureBox.
            if (isDragging)
                g.DrawString("Dragging", Font, System.Drawing.Brushes.Blue, new Point(30, 30));
            //else
            //    g.DrawString("Map view test", Font, System.Drawing.Brushes.Blue, new Point(30, 30));

            foreach (var level in Enum.GetValues(typeof(MapLevel)))
                foreach (var map in subMaps)
                    if ((map.MapLevel == (MapLevel)level) && (map.MapLevel >= minimumDrawLevel))
                        DrawSubMap(g, map);

            // Draw Grid
            for (var x = 0; x < 36000; x += 4000)
                g.DrawLine(x % 4000 == 0 ? System.Drawing.Pens.Gray : System.Drawing.Pens.LightGray, ViewOffset.X + x, 0, ViewOffset.X + x, 36000) ;
            for (var y = 0; y > -36000; y -= 4000)
                g.DrawLine(y % 4000 == 0 ? System.Drawing.Pens.Gray : System.Drawing.Pens.LightGray, 0, ViewOffset.Y + y, 36000, ViewOffset.Y + y);

            /*
            var f = new Font(Font.FontFamily, 100f);
            foreach (var zg in AADB.DB_Zone_Groups.Values)
            {
                if (zg.name.StartsWith("instance"))
                    continue;

                Color col = Color.Cyan;
                switch(zg.id % 4)
                {
                    case 0: col = Color.Cyan; break;
                    case 1: col = Color.LimeGreen; break;
                    case 2: col = Color.Green; break;
                    case 3: col = Color.LightBlue; break;
                }

                var br = new System.Drawing.SolidBrush(col);
                var pn = new System.Drawing.Pen(br);

                var pos = CoordToPixel(zg.PosAndSize.X, zg.PosAndSize.Y);
                g.DrawRectangle(pn, ViewOffset.X + pos.X, ViewOffset.Y + pos.Y - zg.PosAndSize.Height, zg.PosAndSize.Width, zg.PosAndSize.Height);
                g.DrawString(zg.display_textLocalized, f, br, ViewOffset.X + pos.X, ViewOffset.Y + pos.Y);
            }
            */

            // Draw Points of Interest
            foreach (var p in poi)
            {
                DrawCross(g, p.CoordX, p.CoordY, p.PoIColor, p.Name);
            }

            if (cbFocus.Checked)
                g.DrawRectangle(Pens.OrangeRed, ViewOffset.X + FocusBorder.X, ViewOffset.Y - FocusBorder.Y - FocusBorder.Height, FocusBorder.Width, FocusBorder.Height);

        }

        private Bitmap PackedImageToBitmap(string packedFileFolder, string packedFileName)
        {
            if (MainForm.ThisForm.pak.isOpen)
            {
                var fn = packedFileFolder + packedFileName + ".dds" ;

                if (MainForm.ThisForm.pak.FileExists(packedFileFolder + Properties.Settings.Default.DefaultGameLanguage + "/" + packedFileName + ".dds"))
                {
                    fn = packedFileFolder + Properties.Settings.Default.DefaultGameLanguage + "/" + packedFileName + ".dds";
                }

                if (MainForm.ThisForm.pak.FileExists(fn))
                {
                    try
                    {
                        var fStream = MainForm.ThisForm.pak.ExportFileAsStream(fn);
                        var fif = FREE_IMAGE_FORMAT.FIF_DDS;
                        FIBITMAP fiBitmap = FreeImage.LoadFromStream(fStream, ref fif);
                        var bmp = FreeImage.GetBitmap(fiBitmap);

                        return bmp;
                    }
                    catch
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

        }

        private void pView_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var dx = (int)Math.Floor((startDragPos.X - e.X) / viewScale);
                var dy = (int)Math.Floor((startDragPos.Y - e.Y) / viewScale);
                ViewOffset = new Point(startDragOffset.X - dx, startDragOffset.Y - dy);
                pView.Refresh();
            }
            updateStatusBar();
        }

        private void pView_MouseDown(object sender, MouseEventArgs e)
        {
            startDragPos = new Point(e.X, e.Y);
            startDragOffset = new Point(ViewOffset.X, ViewOffset.Y);
            isDragging = true;
            pView.Invalidate();
            updateStatusBar();
        }

        private void pView_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            pView.Invalidate();
            updateStatusBar();
        }

        public MapViewPoI AddPoI(Point coords, string name) => AddPoI(coords.X,coords.Y, name, Color.Yellow);

        public MapViewPoI AddPoI(float x, float y, string name, Color col)
        {
            var newPoi = new MapViewPoI();
            newPoi.CoordX = x;
            newPoi.CoordY = y;
            newPoi.Name = name;
            newPoi.PoIColor = col;
            poi.Add(newPoi);
            return newPoi;
        }

        public void ClearPoI()
        {
            poi.Clear();
        }

        public MapViewMap AddMap(float x, float y, float w, float h, string name, string fileDirectory, string fileName, MapLevel level)
        {
            var newMap = new MapViewMap();
            newMap.ZoneCoordX = x;
            newMap.ZoneCoordY = y;
            newMap.ZoneWidth = w;
            newMap.ZoneHeight = h;
            newMap.Name = name;
            newMap.MapLevel = level;
            newMap.ImageFile = fileName;
            newMap.BitmapImage = PackedImageToBitmap(fileDirectory, fileName);
            newMap.ImgCoordX = x;
            newMap.ImgCoordY = y;
            newMap.ImgWidth = w;
            newMap.ImgHeight = h;

            subMaps.Add(newMap);
            return newMap;
        }

        public void ClearMaps()
        {
            subMaps.Clear();
        }


        private void MapViewForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            ThisForm = null;
        }

        private void cbShowWorldMap_CheckedChanged(object sender, EventArgs e)
        {
            if (cbShowWorldMap.Checked)
                minimumDrawLevel = MapLevel.None;
            else
                minimumDrawLevel = MapLevel.Zone;
            pView.Refresh();
            updateStatusBar();
        }

        private void cbPoINames_CheckedChanged(object sender, EventArgs e)
        {
            pView.Refresh();
            updateStatusBar();
        }

        private void cbZoneBorders_CheckedChanged(object sender, EventArgs e)
        {
            pView.Refresh();
            updateStatusBar();
        }

        public void FocusAllPoIs()
        {
            var first = true;
            FocusBorder = new RectangleF();
            foreach (var p in poi)
            {
                if (first)
                {
                    first = false;
                    FocusBorder.X = p.CoordX;
                    FocusBorder.Y = p.CoordY;
                    FocusBorder.Width = 1;
                    FocusBorder.Height = 1;
                }
                else
                {
                    if (p.CoordX < FocusBorder.X)
                    {
                        FocusBorder.Width = FocusBorder.Width + FocusBorder.X - p.CoordX;
                        FocusBorder.X = p.CoordX;
                    }
                    if (p.CoordX > FocusBorder.Right)
                        FocusBorder.Width = p.CoordX - FocusBorder.Left + 1;

                    if (p.CoordY < FocusBorder.Y)
                    {
                        FocusBorder.Height = FocusBorder.Height + FocusBorder.Y - p.CoordY;
                        FocusBorder.Y = p.CoordY;
                    }
                    if (p.CoordY > FocusBorder.Bottom)
                        FocusBorder.Height = p.CoordY - FocusBorder.Top + 1;
                }
            }

            if ((FocusBorder.Width > 0) && (FocusBorder.Height > 0))
            {
                var center = CoordToPixel(new Point((int)(FocusBorder.X + (FocusBorder.Width / 2f)), (int)(FocusBorder.Y + (FocusBorder.Height / 2))));
                viewOffset.X = (center.X * -1);
                viewOffset.Y = (center.Y * -1) + ((int)FocusBorder.Height / 2);
                viewOffset.X += (int)Math.Round(((float)pView.Width / viewScale) / 2f);
                viewOffset.Y += (int)Math.Round(((float)pView.Height / viewScale) / 2f);
            }
            pView.Refresh();
        }

        private void cbFocus_CheckedChanged(object sender, EventArgs e)
        {
            pView.Refresh();
            updateStatusBar();
        }
    }

    public class MapViewPoI
    {
        public float CoordX = 0f;
        public float CoordY = 0f;
        public Color PoIColor = Color.Yellow;
        public string Name = string.Empty;
    }

    public enum MapLevel : byte
    { 
        None = 0,
        WorldMap = 1,
        Continent = 2,
        Zone = 3,
        City = 4
    }

    public class MapViewMap
    {
        public float ZoneCoordX = 0f;
        public float ZoneCoordY = 0f;
        public float ZoneWidth = 0f;
        public float ZoneHeight = 0f;
        public float ImgCoordX = 0f;
        public float ImgCoordY = 0f;
        public float ImgWidth = 0f;
        public float ImgHeight = 0f;
        public Color MapBorderColor = Color.Yellow;
        public string Name = string.Empty;
        public string ImageFile = string.Empty;
        public Bitmap BitmapImage = null;
        public MapLevel MapLevel = MapLevel.None;
    }

}
