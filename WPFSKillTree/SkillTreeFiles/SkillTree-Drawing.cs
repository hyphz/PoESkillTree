﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace POESKillTree.SkillTreeFiles
{
    public partial class SkillTree
    {
        #region Members

        public List<KeyValuePair<Rect, ImageBrush>> FacesBrush = new List<KeyValuePair<Rect, ImageBrush>>();
        public List<KeyValuePair<Size, ImageBrush>> NodeSurroundBrush = new List<KeyValuePair<Size, ImageBrush>>();
        public DrawingVisual SkillTreeVisual;

        public Dictionary<bool, KeyValuePair<Rect, ImageBrush>> StartBackgrounds =
            new Dictionary<bool, KeyValuePair<Rect, ImageBrush>>();

        public DrawingVisual PicActiveLinks;
        public DrawingVisual PicBackground;
        public DrawingVisual PicFaces;
        public DrawingVisual PicHighlights;
        public DrawingVisual PicLinks;
        public DrawingVisual PicPathOverlay;
        public DrawingVisual PicSkillBaseSurround;
        public DrawingVisual PicSkillIconLayer;
        public DrawingVisual PicActiveSkillIconLayer;
        public DrawingVisual PicSkillSurround;

        public void CreateCombineVisual()
        {
            SkillTreeVisual = new DrawingVisual();
            SkillTreeVisual.Children.Add(PicBackground);
            SkillTreeVisual.Children.Add(PicLinks);
            SkillTreeVisual.Children.Add(PicActiveLinks);
            SkillTreeVisual.Children.Add(PicPathOverlay);
            SkillTreeVisual.Children.Add(PicSkillIconLayer);
            SkillTreeVisual.Children.Add(PicActiveSkillIconLayer);
            SkillTreeVisual.Children.Add(PicSkillBaseSurround);
            SkillTreeVisual.Children.Add(PicSkillSurround);
            SkillTreeVisual.Children.Add(PicFaces);
            SkillTreeVisual.Children.Add(PicHighlights);
        }

        #endregion

        public void ClearPath()
        {
            PicPathOverlay.RenderOpen().Close();
        }

        private void DrawBackgroundLayer()
        {
            PicBackground = new DrawingVisual();
            using (var drawingContext = PicBackground.RenderOpen())
            {
                BitmapImage[] iscr =
                {
                    _assets["PSGroupBackground1"].PImage, 
                    _assets["PSGroupBackground2"].PImage,
                    _assets["PSGroupBackground3"].PImage
                };
                var orbitBrush = new Brush[3];
                orbitBrush[0] = new ImageBrush(_assets["PSGroupBackground1"].PImage);
                orbitBrush[1] = new ImageBrush(_assets["PSGroupBackground2"].PImage);
                orbitBrush[2] = new ImageBrush(_assets["PSGroupBackground3"].PImage);
                ((ImageBrush) orbitBrush[2]).TileMode = TileMode.FlipXY;
                ((ImageBrush) orbitBrush[2]).Viewport = new Rect(0, 0, 1, .5f);

                var backgroundBrush = new ImageBrush(_assets["Background1"].PImage) {TileMode = TileMode.Tile};
                backgroundBrush.Viewport = new Rect(0, 0, 
                    6 * backgroundBrush.ImageSource.Width / TRect.Width, 
                    6 * backgroundBrush.ImageSource.Height / TRect.Width);
                drawingContext.DrawRectangle(backgroundBrush, null, TRect);

                var topGradient = new LinearGradientBrush();
                topGradient.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
                topGradient.GradientStops.Add(new GradientStop(new Color(), 0.0));
                topGradient.StartPoint = new Point(0, 1);
                topGradient.EndPoint = new Point(0, 0);

                var leftGradient = new LinearGradientBrush();
                leftGradient.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
                leftGradient.GradientStops.Add(new GradientStop(new Color(), 0.0));
                leftGradient.StartPoint = new Point(1, 0);
                leftGradient.EndPoint = new Point(0, 0);

                var bottomGradient = new LinearGradientBrush();
                bottomGradient.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
                bottomGradient.GradientStops.Add(new GradientStop(new Color(), 0.0));
                bottomGradient.StartPoint = new Point(0, 0);
                bottomGradient.EndPoint = new Point(0, 1);

                var rightGradient = new LinearGradientBrush();
                rightGradient.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
                rightGradient.GradientStops.Add(new GradientStop(new Color(), 0.0));
                rightGradient.StartPoint = new Point(0, 0);
                rightGradient.EndPoint = new Point(1, 0);

                const int gradientWidth = 200;
                var topRect = new Rect2D(TRect.Left, TRect.Top, TRect.Width, gradientWidth);
                var leftRect = new Rect2D(TRect.Left, TRect.Top, gradientWidth, TRect.Height);
                var bottomRect = new Rect2D(TRect.Left, TRect.Bottom - gradientWidth, TRect.Width, gradientWidth);
                var rightRect = new Rect2D(TRect.Right - gradientWidth, TRect.Top, gradientWidth, TRect.Height);

                drawingContext.DrawRectangle(topGradient, null, topRect);
                drawingContext.DrawRectangle(leftGradient, null, leftRect);
                drawingContext.DrawRectangle(bottomGradient, null, bottomRect);
                drawingContext.DrawRectangle(rightGradient, null, rightRect);
                foreach (var skillNodeGroup in NodeGroups)
                {
                    if (skillNodeGroup.OcpOrb == null)
                        skillNodeGroup.OcpOrb = new Dictionary<int, bool>();
                    var cgrp = skillNodeGroup.OcpOrb.Keys.Where(ng => ng <= 3);
                    if (!cgrp.Any()) continue;
                    var maxr = cgrp.Max(ng => ng);
                    if (maxr == 0) continue;
                    maxr = maxr > 3 ? 2 : maxr - 1;
                    var maxfac = maxr == 2 ? 2 : 1;
                    drawingContext.DrawRectangle(orbitBrush[maxr], null,
                        new Rect(
                            skillNodeGroup.Position -
                            new Vector2D(iscr[maxr].PixelWidth * 1.25, iscr[maxr].PixelHeight * 1.25 * maxfac),
                            new Size(iscr[maxr].PixelWidth * 2.5, iscr[maxr].PixelHeight * 2.5 * maxfac)));
                }
            }
        }

        private void DrawConnection(DrawingContext dc, Pen pen2, SkillNode n1, SkillNode n2)
        {
            if (n1.SkillNodeGroup == n2.SkillNodeGroup && n1.Orbit == n2.Orbit)
            {
                if (n1.Arc - n2.Arc > 0 && n1.Arc - n2.Arc <= Math.PI ||
                    n1.Arc - n2.Arc < -Math.PI)
                {
                    dc.DrawArc(null, pen2, n1.Position, n2.Position,
                        new Size(SkillNode.OrbitRadii[n1.Orbit],
                            SkillNode.OrbitRadii[n1.Orbit]));
                }
                else
                {
                    dc.DrawArc(null, pen2, n2.Position, n1.Position,
                        new Size(SkillNode.OrbitRadii[n1.Orbit],
                            SkillNode.OrbitRadii[n1.Orbit]));
                }
            }
            else
            {
                dc.DrawLine(pen2, n1.Position, n2.Position);
            }
        }

        public void DrawFaces()
        {
            using (DrawingContext dc = PicFaces.RenderOpen())
            {
                for (int i = 0; i < CharName.Count; i++)
                {
                    string s = CharName[i];
                    Vector2D pos = Skillnodes.First(nd => nd.Value.Name.ToUpper() == s.ToUpper()).Value.Position;
                    dc.DrawRectangle(StartBackgrounds[false].Value, null,
                        new Rect(
                            pos - new Vector2D(StartBackgrounds[false].Key.Width, StartBackgrounds[false].Key.Height),
                            pos + new Vector2D(StartBackgrounds[false].Key.Width, StartBackgrounds[false].Key.Height)));
                    if (_chartype == i)
                    {
                        dc.DrawRectangle(FacesBrush[i].Value, null,
                            new Rect(pos - new Vector2D(FacesBrush[i].Key.Width, FacesBrush[i].Key.Height),
                                pos + new Vector2D(FacesBrush[i].Key.Width, FacesBrush[i].Key.Height)));

                        var charBaseAttr = CharBaseAttributes[Chartype];

                        var text = CreateAttributeText(charBaseAttr["+# to Intelligence"].ToString(CultureInfo.InvariantCulture), Brushes.DodgerBlue);
                        dc.DrawText(text, pos - new Vector2D(19, 117));

                        text = CreateAttributeText(charBaseAttr["+# to Strength"].ToString(CultureInfo.InvariantCulture), Brushes.IndianRed);
                        dc.DrawText(text, pos - new Vector2D(102, -32));

                        text = CreateAttributeText(charBaseAttr["+# to Dexterity"].ToString(CultureInfo.InvariantCulture), Brushes.MediumSeaGreen);
                        dc.DrawText(text, pos - new Vector2D(-69, -32));

                    }
                }
            }
        }

        private FormattedText CreateAttributeText(string text, SolidColorBrush colorBrush)
        {
            return new FormattedText(text,
                new CultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal,
                    new FontStretch()),
                30, colorBrush);
        }

        public void DrawHighlights(List<SkillNode> nodes, SolidColorBrush brush = null)
        {
            var hpen = new Pen(brush ?? Brushes.Aqua, 20);
            using (DrawingContext dc = PicHighlights.RenderOpen())
            {
                foreach (SkillNode node in nodes)
                {
                    dc.DrawEllipse(null, hpen, node.Position, 80, 80);
                }
            }
        }

        private void DrawLinkBackgroundLayer(List<ushort[]> links)
        {
            PicLinks = new DrawingVisual();
            var pen2 = new Pen(Brushes.DarkSlateGray, 20f);
            using (DrawingContext dc = PicLinks.RenderOpen())
            {
                foreach (var nid in links)
                {
                    SkillNode n1 = Skillnodes[nid[0]];
                    SkillNode n2 = Skillnodes[nid[1]];
                    DrawConnection(dc, pen2, n1, n2);
                    //if (n1.SkillNodeGroup == n2.SkillNodeGroup && n1.orbit == n2.orbit)
                    //{
                    //    if (n1.Arc - n2.Arc > 0 && n1.Arc - n2.Arc < Math.PI || n1.Arc - n2.Arc < -Math.PI)
                    //    {
                    //        dc.DrawArc(null, pen2, n1.Position, n2.Position,
                    //                   new Size(SkillTree.SkillNode.orbitRadii[n1.orbit],
                    //                            SkillTree.SkillNode.orbitRadii[n1.orbit]));
                    //    }
                    //    else
                    //    {
                    //        dc.DrawArc(null, pen2, n2.Position, n1.Position,
                    //                   new Size(SkillTree.SkillNode.orbitRadii[n1.orbit],
                    //                            SkillTree.SkillNode.orbitRadii[n1.orbit]));
                    //    }
                    //}
                    //else
                    //{
                    //    dc.DrawLine(pen2, n1.Position, n2.Position);
                    //}
                }
            }
        }

        private void DrawNodeBaseSurround()
        {
            using (DrawingContext dc = PicSkillBaseSurround.RenderOpen())
            {
                foreach (ushort skillNode in Skillnodes.Keys)
                {
                    Vector2D pos = (Skillnodes[skillNode].Position);

                    if (Skillnodes[skillNode].IsNotable)
                    {
                        dc.DrawRectangle(NodeSurroundBrush[3].Value, null,
                            new Rect((int)pos.X - NodeSurroundBrush[3].Key.Width,
                                (int)pos.Y - NodeSurroundBrush[3].Key.Height,
                                NodeSurroundBrush[3].Key.Width * 2,
                                NodeSurroundBrush[3].Key.Height * 2));
                    }
                    else if (Skillnodes[skillNode].IsKeyStone)
                    {
                        dc.DrawRectangle(NodeSurroundBrush[2].Value, null,
                            new Rect((int)pos.X - NodeSurroundBrush[2].Key.Width,
                                (int)pos.Y - NodeSurroundBrush[2].Key.Height,
                                NodeSurroundBrush[2].Key.Width * 2,
                                NodeSurroundBrush[2].Key.Height * 2));
                    }
                    else if (Skillnodes[skillNode].IsMastery)
                    {
                        //Needs to be here so that "Masteries" (Middle images of nodes) don't get anything drawn around them.
                    }
                    else
                        dc.DrawRectangle(NodeSurroundBrush[0].Value, null,
                            new Rect((int)pos.X - NodeSurroundBrush[0].Key.Width,
                                (int)pos.Y - NodeSurroundBrush[0].Key.Height,
                                NodeSurroundBrush[0].Key.Width * 2,
                                NodeSurroundBrush[0].Key.Height * 2));
                }
            }
        }

        private void DrawNodeSurround()
        {
            using (DrawingContext dc = PicSkillSurround.RenderOpen())
            {
                foreach (ushort skillNode in SkilledNodes)
                {
                    Vector2D pos = (Skillnodes[skillNode].Position);

                    if (Skillnodes[skillNode].IsNotable)
                    {
                        dc.DrawRectangle(NodeSurroundBrush[5].Value, null,
                            new Rect((int)pos.X - NodeSurroundBrush[5].Key.Width,
                                (int)pos.Y - NodeSurroundBrush[5].Key.Height,
                                NodeSurroundBrush[5].Key.Width * 2,
                                NodeSurroundBrush[5].Key.Height * 2));
                    }
                    else if (Skillnodes[skillNode].IsKeyStone)
                    {
                        dc.DrawRectangle(NodeSurroundBrush[4].Value, null,
                            new Rect((int)pos.X - NodeSurroundBrush[4].Key.Width,
                                (int)pos.Y - NodeSurroundBrush[4].Key.Height,
                                NodeSurroundBrush[4].Key.Width * 2,
                                NodeSurroundBrush[4].Key.Height * 2));
                    }
                    else if (Skillnodes[skillNode].IsMastery)
                    {
                        //Needs to be here so that "Masteries" (Middle images of nodes) don't get anything drawn around them.
                    }
                    else
                        dc.DrawRectangle(NodeSurroundBrush[1].Value, null,
                            new Rect((int)pos.X - NodeSurroundBrush[1].Key.Width,
                                (int)pos.Y - NodeSurroundBrush[1].Key.Height,
                                NodeSurroundBrush[1].Key.Width * 2,
                                NodeSurroundBrush[1].Key.Height * 2));
                }
            }
        }

        public void DrawPath(List<ushort> path)
        {
            var pen2 = new Pen(Brushes.LawnGreen, 15f) {DashStyle = new DashStyle(new DoubleCollection {2}, 2)};

            using (DrawingContext dc = PicPathOverlay.RenderOpen())
            {
                for (int i = -1; i < path.Count - 1; i++)
                {
                    SkillNode n1 = i == -1
                        ? Skillnodes[path[i + 1]].Neighbor.First(sn => SkilledNodes.Contains(sn.Id))
                        : Skillnodes[path[i]];
                    SkillNode n2 = Skillnodes[path[i + 1]];

                    DrawConnection(dc, pen2, n1, n2);
                }
            }
        }

        public void DrawRefundPreview(HashSet<ushort> nodes)
        {
            var pen2 = new Pen(Brushes.Red, 15f) {DashStyle = new DashStyle(new DoubleCollection {2}, 2)};

            using (DrawingContext dc = PicPathOverlay.RenderOpen())
            {
                foreach (ushort node in nodes)
                {
                    foreach (SkillNode n2 in Skillnodes[node].Neighbor)
                    {
                        if (SkilledNodes.Contains(n2.Id) && (node < n2.Id || !(nodes.Contains(n2.Id))))
                            DrawConnection(dc, pen2, Skillnodes[node], n2);
                    }
                }
            }
        }

        private void InitSkillIconLayers()
        {
            PicActiveSkillIconLayer = new DrawingVisual();
            PicSkillIconLayer = new DrawingVisual();
        }

        private void DrawSkillIconLayer()
        {
            var pen = new Pen(Brushes.Black, 5);
            
            Geometry g = new RectangleGeometry(TRect);
            using (DrawingContext dc = PicSkillIconLayer.RenderOpen())
            {
                dc.DrawGeometry(null, pen, g);
                foreach (var skillNode in Skillnodes)
                {
                    Size isize;
                    var imageBrush = new ImageBrush();
                    var rect = IconInActiveSkills.SkillPositions[skillNode.Value.IconKey].Key;
                    var bitmapImage = IconInActiveSkills.Images[IconInActiveSkills.SkillPositions[skillNode.Value.IconKey].Value];
                    imageBrush.Stretch = Stretch.Uniform;
                    imageBrush.ImageSource = bitmapImage;

                    imageBrush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
                    imageBrush.Viewbox = new Rect(rect.X / bitmapImage.PixelWidth, rect.Y / bitmapImage.PixelHeight, rect.Width / bitmapImage.PixelWidth,
                        rect.Height / bitmapImage.PixelHeight);
                    Vector2D pos = (skillNode.Value.Position);
                    dc.DrawEllipse(imageBrush, null, pos, rect.Width, rect.Height);
                }
            }
        }

        private void DrawActiveNodeIcons()
        {
            var pen = new Pen(Brushes.Black, 5);
           
            Geometry g = new RectangleGeometry(TRect);
            using (DrawingContext dc = PicActiveSkillIconLayer.RenderOpen())
            {
                dc.DrawGeometry(null, pen, g);
                foreach (var skillNode in SkilledNodes)
                {
                    Size isize;
                    var imageBrush = new ImageBrush();
                    var rect = IconActiveSkills.SkillPositions[Skillnodes[skillNode].IconKey].Key;
                    var bitmapImage = IconActiveSkills.Images[IconActiveSkills.SkillPositions[Skillnodes[skillNode].IconKey].Value];
                    imageBrush.Stretch = Stretch.Uniform;
                    imageBrush.ImageSource = bitmapImage;

                    imageBrush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
                    imageBrush.Viewbox = new Rect(rect.X / bitmapImage.PixelWidth, rect.Y / bitmapImage.PixelHeight, rect.Width / bitmapImage.PixelWidth,
                        rect.Height / bitmapImage.PixelHeight);
                    Vector2D pos = (Skillnodes[skillNode].Position);
                    dc.DrawEllipse(imageBrush, null, pos, rect.Width, rect.Height);
                }
            }
        }

        private void InitFaceBrushesAndLayer()
        {
            foreach (string faceName in FaceNames)
            {
                var bi = ImageHelper.OnLoadBitmapImage(new Uri("Data\\Assets\\" + faceName + ".png", UriKind.Relative));
                FacesBrush.Add(new KeyValuePair<Rect, ImageBrush>(new Rect(0, 0, bi.PixelWidth, bi.PixelHeight),
                    new ImageBrush(bi)));
            }

            var bi2 = ImageHelper.OnLoadBitmapImage(new Uri("Data\\Assets\\PSStartNodeBackgroundInactive.png", UriKind.Relative));
            StartBackgrounds.Add(false,
                (new KeyValuePair<Rect, ImageBrush>(new Rect(0, 0, bi2.PixelWidth, bi2.PixelHeight),
                    new ImageBrush(bi2))));
            PicFaces = new DrawingVisual();
        }

        private void InitNodeSurround()
        {
            PicSkillSurround = new DrawingVisual();
            PicSkillBaseSurround = new DrawingVisual();
            var brNot = new ImageBrush {Stretch = Stretch.Uniform};
            BitmapImage pImageNot = _assets[NodeBackgrounds["notable"]].PImage;
            brNot.ImageSource = pImageNot;
            var sizeNot = new Size(pImageNot.PixelWidth, pImageNot.PixelHeight);


            var brKs = new ImageBrush {Stretch = Stretch.Uniform};
            BitmapImage pImageKr = _assets[NodeBackgrounds["keystone"]].PImage;
            brKs.ImageSource = pImageKr;
            Size sizeKs = new Size(pImageKr.PixelWidth, pImageKr.PixelHeight);

            var brNotH = new ImageBrush {Stretch = Stretch.Uniform};
            BitmapImage pImageNotH = _assets[NodeBackgroundsActive["notable"]].PImage;
            brNotH.ImageSource = pImageNotH;
            Size sizeNotH = new Size(pImageNotH.PixelWidth, pImageNotH.PixelHeight);


            var brKsh = new ImageBrush {Stretch = Stretch.Uniform};
            BitmapImage pImageKrH = _assets[NodeBackgroundsActive["keystone"]].PImage;
            brKsh.ImageSource = pImageKrH;
            Size sizeKsH = new Size(pImageKrH.PixelWidth, pImageKrH.PixelHeight);

            var brNorm = new ImageBrush {Stretch = Stretch.Uniform};
            BitmapImage pImageNorm = _assets[NodeBackgrounds["normal"]].PImage;
            brNorm.ImageSource = pImageNorm;
            Size isizeNorm = new Size(pImageNorm.PixelWidth, pImageNorm.PixelHeight);

            var brNormA = new ImageBrush {Stretch = Stretch.Uniform};
            BitmapImage pImageNormA = _assets[NodeBackgroundsActive["normal"]].PImage;
            brNormA.ImageSource = pImageNormA;
            Size isizeNormA = new Size(pImageNormA.PixelWidth, pImageNormA.PixelHeight);

            NodeSurroundBrush.Add(new KeyValuePair<Size, ImageBrush>(isizeNorm, brNorm));
            NodeSurroundBrush.Add(new KeyValuePair<Size, ImageBrush>(isizeNormA, brNormA));
            NodeSurroundBrush.Add(new KeyValuePair<Size, ImageBrush>(sizeKs, brKs));
            NodeSurroundBrush.Add(new KeyValuePair<Size, ImageBrush>(sizeNot, brNot));
            NodeSurroundBrush.Add(new KeyValuePair<Size, ImageBrush>(sizeKsH, brKsh));
            NodeSurroundBrush.Add(new KeyValuePair<Size, ImageBrush>(sizeNotH, brNotH));
        }

        private void InitOtherDynamicLayers()
        {
            PicActiveLinks = new DrawingVisual();
            PicPathOverlay = new DrawingVisual();
            PicHighlights = new DrawingVisual();
        }
    }
}