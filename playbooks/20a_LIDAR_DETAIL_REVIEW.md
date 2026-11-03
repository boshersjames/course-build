# LiDAR detail review: what Tierra Rejada taught us about reading the LiDAR

Written 2026-10-31 from the Tierra Rejada build notes (course-build/progress/tierra, BOJA_FEEDBACK_CHECKLIST.md, the per-hole gate lists, the scan reports and the Oct 29 visit notes). Purpose: before any new course is built, run these decision points against its LiDAR, satellite and OSM data, so we stop guessing on things that cost hours of rework at Tierra.

Each decision point has the same shape, so the photo check can be added later without rewriting the list:

- **Question:** the thing a builder must decide before building.
- **LiDAR says / can't say:** what the point cloud and the 1 m heightmap actually show here.
- **Tierra case:** where we got it wrong, and what it cost.
- **Check now:** the data check that answers it from LiDAR, satellite and OSM alone.
- **Photo check (slot):** what BoJa's photo or scan has to show to settle it. Empty until the photo pass is paired with this list.
- **Default:** what gets built when nobody answers. Defaults never invent ground.

## 1. The data we actually have

| Fact | Tierra value | Why it matters |
|---|---|---|
| Survey | USGS SoCal Wildfires B3, flown May to July 2018, QL2 | The course is 8 years older than the scan. Anything rebuilt, planted or moved since is wrong. |
| Density | 4.1 pts/m² all returns, 3.3 pts/m² ground | About one ground point every 55 to 70 cm, smoothed over about 1 m. |
| Heightmap | 2049 px over 2000 m, 0.98 m cells, lightly smoothed, gaps interpolated | Nothing under about 1 m wide or 5 cm tall is real in the heightmap. |
| Ground coverage | 86.5 % of cells have a ground return; largest filled gap 16 m | Under tree lines and buildings the ground is invented by interpolation. |
| Water | No returns on open water; the flat water reads at one level with a clean gap in the histogram | Water levels are reliable. Water edges under canopy are not. |
| Vegetation | Non-ground returns above 0.5 m make the brush mask; tree crowns from returns above about 3 m | Grass height and species are not in the LiDAR. |
| Geo frame | EPSG:6340, 2000 m OPCD square, SW corner 328865.84 E / 3792716.77 N, Unity y = elevation minus 182 m | Every other input (OSM, satellite, phone scans, photo GPS) is only as good as its registration to this frame. |

The single most expensive habit at Tierra was treating the LiDAR as if it resolved features below its grid (bunker faces, collars, cliff faces, tee decks, pond lips) and then "finishing" those features from imagination. The second was treating other inputs (OSM outlines, the satellite drape, the phone GPS) as if they were registered to the LiDAR when they were off by 1.5 to 6 m.

## 2. The misreads, ranked by what they cost

Rough rework cost is from the notes: how many passes, editors and BoJa reviews each one took before it was right.

| Rank | Misread | Hole(s) | What it cost |
|---|---|---|---|
| 1 | Hillsides and banks built as mown turf or green scrub where the real cover is grey dormant sage on bare soil | 10/18, 11, 12, 14, 16, 17, 18, 1, 4, 8 | Four course-wide vegetation passes, a native-slopes pass, hillside redo per hole, and BoJa's "I gave you the right hillside, you didn't look" |
| 2 | Bunkers too shallow and too small; faces flat because the 1 m grid flattens them; lips and islands guessed | 10, 11, 12, 15, 16, 17, 18, 9 | Bunker regrade pass, scan apply, per-bunker photo pass, an invented bunker on 16, repeated face-height questions |
| 3 | Greens built from the 2018 LiDAR as if that were the surface; then two nights of phone scan and COLMAP work to replace it | 10 to 18 | Scan pipeline, failed COLMAP runs on 17 and 18, 13 and 16 scans unusable, 12 half covered, pins re-checked |
| 4 | Hole 14 pond: water plane 0.47 m above the LiDAR water, a brown "lip" slab hiding the SW lobe, hazard polygon 2 to 3.5 m short | 14 | Two review rounds, a measure-only session, hazard rebuild, and the "lake position is still off" note from BoJa |
| 5 | Green and bunker meshes offset from the LiDAR pad (OSM outlines 1.5 to 3 m off) | 18, 14 | Position studies on both holes, pins and hazards to move, and a hole 14 result that the satellite contradicts |
| 6 | Outer ground: the land past the course square left as old satellite drape, an invented horizon, Highway 23 read as a sandy wash | 15, 16, 12, 11, 18 | Road, entrance and freeway builds, a horizon rebuild, flyover re-renders |
| 7 | Sandstone cliffs on 11 and 18 built as a flat drape, then as rock on the wrong cells (mask rows flipped), then three rock passes | 11, 18 | Five cliff passes and the rock face still "not a match" |
| 8 | Rough mesh drawn over the collar and green edge, so the green looked 4 m from the bunker lip | 11, 12 (collar skirt) | A mesh surgery package, a second one for the skirt slivers |
| 9 | Tee pads: a tee box mapped as a green (5 Black), an unused pad sunk into a hill as a "crater" (4), a LiDAR pad OSM never mapped (17 front), tiered decks flattened (18) | 4, 5, 17, 18, 13 | Pad cuts, GKD moves, a crater fill, tee corridor sweeps |
| 10 | Trees and shrubs: 2,714 shrubs of 3 m or more planted as pale "blossom trees"; species assigned by a height rule; crowns rendered 1.3x the LiDAR crown | 15, 16, 18, 12, 11, 9/1 | Shrub atlas swap, pepper model built three times, per-tree swaps on every hole |
| 11 | Creek and stream on 13 as a flat water sheet at grade; the real creek is a narrow channel in rock | 13, 15 | Creek rebuilt twice, bridges and cascade rebuilt |
| 12 | Props placed by eye: pump station 40 m off, the hole 14 enclosure 9.8 m off, restroom door direction guessed | 13, 14 | Measure-only sessions, re-posing every photo that framed them |
| 13 | Collars, mow lines and surrounds assumed from nothing (0.12 m blend straight into rough) | all | Collar pass across 18 greens, seam fixes, "bright outline ring" fixes |

## 3. Decision points

Work through these in order for each new course. Record the answer per hole in a `decisions.csv` next to the course data (columns: point, hole, answer, source, photo, decided_by, date). A point with no answer gets its default and is marked `default`.

### Ground cover (misread 1)

**D1. Is each out-of-play cell mown turf, dry grass, scrub on soil, bare soil, or rock?**
- *LiDAR says:* slope, and whether there are vegetation returns above 0.5 m (brush mask) or above 3 m (shrubs and trees). It can't tell 0.3 m dormant grass from mown turf: almost every grass return is classed as ground.
- *Tierra case:* the OSM rough band was generated around every hole, so 85 % of the 2 km square ended up as "course rough" meshes coloured by the satellite. Hillsides between 10 and 18, behind 11, around the 12 path, the 14 tee bank, right of 17 and above the 18 tee all rendered as smeared mown rough or green with scattered shrubs on turf. A course-wide clear of grass tufts from in-play rough also stripped these banks. The photos show grey-brown coastal sage on bare soil with 5 % or less warm soil showing, plus prickly pear, yucca and dead brush.
- *Check now:* per cell, in this order. (a) Inside an OSM tee, green, fairway or bunker: turf. (b) Brush mask hit (returns 0.5 to 3 m): scrub on soil. (c) Slope over 15° and outside the mown corridor: dry grass or scrub, never mown rough. (d) Satellite tone tan or grey-brown, and 12 m or more from a fairway: dry grass. (e) Everything else inside the corridor: mown rough. Measure cover share, not plant count.
- *Photo check (slot):* a photo across each hillside at eye level. Pass when the warm bare soil share in the render is within 5 points of the photo's and the scrub colour (r/g, b/g) matches.
- *Default:* scrub on grey-brown litter at 70 to 80 % cover on any slope over 15° outside the corridor. No shrubs on mown turf, ever.

**D2. Where does the mown corridor end?**
- *LiDAR says:* nothing. The mow line is a texture change, not a height change.
- *Tierra case:* the hole 10/18 bank reached 5 to 7 m into hole 10's rough; the "pale ground right of 15" was partly a real road and dirt lot.
- *Check now:* trace the mown edge from the satellite (green turf against tan), snap it to the OSM rough polygon only where the two agree within 3 m, and keep a toe polyline per bank.
- *Photo check (slot):* a photo along the toe. Pass when the toe line in the render lands within 1 m of the photo's.
- *Default:* the satellite edge.

**D3. Steep cells: rock face or scrub?**
- *LiDAR says:* slope only, and the 1 m grid smooths near-vertical sandstone to 35 to 70 %. The cliff cells are the right place, but the face shape is not in the data.
- *Tierra case:* 11 and 18 cliffs were a flat drape, then rock over the whole ridge (mask rows flipped on the first build, so rock landed on the wrong cells), then rock on all cells over 35°, which put a white slab on the skyline. The photos show bare rock only on one tan outcrop (4656) and the pale lower face (4665), with scrub everywhere else.
- *Check now:* cells over 35° inside the brush mask are scrub; cells over 35° with no vegetation returns and a pale satellite tone are candidate rock. Render the candidate mask over the satellite before building, and check the mask orientation on a known feature (a cart path or a building) every time a mask is built.
- *Photo check (slot):* the photo that frames the face. Pass when the rock extent matches the photo's bare-rock pixels and the face is below the skyline.
- *Default:* scrub, with rock only where the satellite is pale and the slope is over 35°.

### Bunkers (misread 2)

**D4. Where is the sand edge?**
- *LiDAR says:* a shallow depression 1 m or more across, with the edge blurred by about a cell. The OSM polygon is somebody's trace of imagery.
- *Tierra case:* the hole 10 fairway bunker scan put sand well outside both model outlines (bunker 24: 142 m² inside the outline, 184 m² outside). Hole 9 had a greenside bunker right of the line where the photos show both front-left. Hole 16 had an extra bunker on the brush slope NW of the green that doesn't exist. Penmar hole 1 had a bunker missing from OSM.
- *Check now:* overlay OSM bunkers on the satellite and on the LiDAR depression mask (local minimum more than 0.25 m below a 10 m ring). A bunker is kept only if the satellite shows sand there. Sand seen in the satellite with no OSM polygon gets traced. Flag any bunker whose LiDAR depression centre is more than 1.5 m from the polygon centre.
- *Photo check (slot):* any photo that frames the bunker. Pass when the outline, lip position and sand line agree. A clean phone scan wins 100 % (BoJa, 10/29).
- *Default:* the satellite sand edge, not the OSM polygon.

**D5. How deep, and how steep is the face?**
- *LiDAR says:* floor depth only where the bunker is wider than about 3 m, and it under-reads every face. Hole 10's scan read both fairway bunkers 0.6 to 0.8 m deeper rim-to-floor than the model. On hole 11 the greenside faces still read three times flatter than the photos (height/width 0.07 to 0.08 against 0.21 to 0.25) even though "the LiDAR supports the current shape".
- *Tierra case:* bunkers were first graded at 0.25 / 0.35 / 0.5 m, then 0.8 to 1.4 m, then re-done per photo. 16's fairway bunkers got a tall turf wall where the photos show sand flashed up the far face to a thin lip. BoJa's rule after Hansen: greenside 0.8 to 1.2 m below the green with a 0.2 to 0.4 m lip and a steep green-side face; fairway 0.4 to 0.7 m; flat floor.
- *Check now:* depth by type as above, floor flat under 3°, face on the green or high side. Treat the LiDAR depth as a floor, never a ceiling: build at least the LiDAR depth plus 0.3 m.
- *Photo check (slot):* face height over width measured in the photo from the fairway side. Pass within 20 %. Photos win over the LiDAR on faces, banks and lips, even beyond the LiDAR (BoJa's photos-win rule, hole 16). Green surfaces never change.
- *Default:* the Hansen rule depths with a steep green-side face and sand flashed up the far face.

**D6. Grass inside the sand: island, nose, or nothing?**
- *LiDAR says:* nothing at this scale.
- *Tierra case:* islands were cut into 11, 18 and 15 from the satellite, then banned ("no turf islands inside bunkers"), then 11 got a connected nose and 15 got two raised mounds because the photos show them.
- *Check now:* no island unless the satellite shows turf fully inside the sand and a photo confirms it.
- *Photo check (slot):* a photo across the bunker. Pass when the grass shape is connected to the bank in the same place.
- *Default:* no island, no nose.

### Greens (misread 3)

**D7. Which surface is the green built from?**
- *LiDAR says:* tiers, ridges, false fronts and the overall fall are real; anything under about 1 m across or 5 cm tall is not. Slopes are overstated near edges. The surface is from 2018.
- *Tierra case:* phone LAS scans replaced the LiDAR on 12 to 15 (fit 3 to 9 cm). 13 drifted (ground stacked two to three times), 16 was 43 % doubled, 11 fitted at 13.6 cm, 12 lost 45 %. Photo-only reconstruction (COLMAP) gave 110 m² of patchy surface on 17 and registered 106 of 430 photos on 18: putting surfaces have too little texture for photos. BoJa: hole 17's whole green sits on a hill and the undulation is the hole's character, so capture it.
- *Check now:* use a scan only when fit MAD is 5 cm or under, with no doubled areas, over at least 80 % of the green. Otherwise keep the LiDAR and list the green for a rescan. Never smooth a green below the LiDAR relief; flag greens over 3 % at any pin.
- *Photo check (slot):* not a photo. A 3D Scanner app LAS per green, walked the way 13 to 15 were. Photos only inform surrounds.
- *Default:* the LiDAR surface, with the slope report attached to the hole so pins avoid anything over 3 %.

**D8. Is the green outline where the LiDAR pad is?**
- *LiDAR says:* the flat pad (slope under 4 ° connected to the green) is usually the putting surface plus collar. But a flat approach strip reads as pad too.
- *Tierra case:* hole 18's green was 3 m west of its pad (inside-slope std 5.2 % at zero shift, 2.5 % at +3 m east; the west edge sat on a 26 % bank) and the greenside bunker 1.5 m west of its hollow. Hole 14's LiDAR fit said +5.6 m east, but the satellite fits the scene green within 0.4 m, because the "pad" there is the flat approach strip between the green and the pond. The phone GPS (3 to 5 m) can't settle a 1.6 m question.
- *Check now:* for every green, run the shift fit (minimise slope std inside the outline, maximise it on a 3 m ring) and the satellite green-tone fit. Both agree within 1 m: fine. They disagree: the satellite decides the putting surface and the LiDAR decides the banks and bunker hollows, and the hole is flagged for a scan georef.
- *Photo check (slot):* a photo from a known collar point with a landmark in frame, or the green scan's own georef. Pass when the collar lands within 1 m.
- *Default:* satellite for the outline, LiDAR for the surround shape. Pins and hazards move with the green.

**D9. Collar, cut step and surround profile.**
- *LiDAR says:* nothing. The real cut step (about 1 cm) is below even the phone scans' noise.
- *Tierra case:* every green ended in a 0.12 m blend straight into rough; collars were added on 14 greens from the one scan that had colour (hole 10: 1.2 m, 1.5 cm step); seams of up to 14 cm showed where collar edges sat above the rough (hole 1); BoJa wants one collar transition on all 18 greens, with banks still following each green's LiDAR; rough close behind 6, 8, 15 and 18 had to become fairway-height collar.
- *Check now:* collar 1.2 m at 1.5 cm proud, fairway height 3 m at the sides and 5 m behind on flat surrounds, rough close in on uphill banks, deep rough 6 m off every green. After building, scan for height gaps between collar and rough over 3 cm.
- *Photo check (slot):* a photo of the collar from the fringe. Pass when the collar width in the render matches the photo within 0.3 m and reads lighter than the green and darker than the rough.
- *Default:* the hole 10 profile above.

**D10. Do we go beyond the LiDAR around a green?**
- *LiDAR says:* the bank behind a green is smoothed; a crest or a bank can be under-read by 0.2 to 0.5 m (hole 10 east back bank +0.3 m in the scan; hole 9 crest needed relief x1.3 to 1.5).
- *Tierra case:* hole 11's bunker bank could only match the photos by rising beyond the scan; hole 16's north bank was lowered to the photo beyond the LiDAR under the photos-win rule; hole 18 mounds behind the green were refused because "terrain is never sculpted beyond the LiDAR".
- *Check now:* terrain is changed only by re-importing the LiDAR. Banks, lips and bunker faces may exceed it where a photo shows it. Green surfaces and contours never change.
- *Photo check (slot):* the photo that shows the bank. Pass when the bank's height over width matches within 20 %.
- *Default:* the LiDAR.

### Water (misread 4)

**D11. Water level, and how far the water reaches.**
- *LiDAR says:* this is one of the things it does well. Open water has no returns, so the lowest connected flat level around the gap is the water level, with a clean gap in the height histogram above it (hole 14: flat at 24.7 to 24.8 m, nothing between 24.9 and 25.0).
- *Tierra case:* the hole 14 water plane sat at 25.22 m, 0.47 m above the LiDAR water, so it flooded 0.5 to 2 m past the real bank; a brown "PondLip" slab was laid to hide the overflow, and the slab covered the whole SW lobe (290 m², a third of the pond). The GKD hazard was 2 to 3.5 m short at the SW tip, and a mound sat on 9 m² of water. The satellite's NE tip leaked into canopy shadow.
- *Check now:* water plane at the LiDAR flat level, never above. Water outline = heightmap cells below that level, connected to the pond. Cross-check against the satellite dark-water mask within 4 m, and distrust it under canopy. Hazard polygon follows the water edge within 1 m. No slab, band or lip object may cover water. A thin eroded cut 0.3 to 1 m wide is the bank.
- *Photo check (slot):* a photo along the bank. Pass when the water edge back-projected onto the water plane lies within 1.5 m of the LiDAR edge.
- *Default:* LiDAR level and LiDAR outline.

**D12. Streams and creeks: channel or sheet?**
- *LiDAR says:* a creek narrower than 2 m shows as a slight dip or nothing, and under trees the ground is interpolated. Water in a channel has no returns.
- *Tierra case:* the 13 creek was built as a wide flat dark-blue sheet at grade with box bridges; the real creek is a narrow rocky channel with water over stones, concrete faux-rock edging, a plank footbridge and a boulder sill. The stream on 15 was buried under the course meshes and read as grass.
- *Check now:* trace the channel from OSM waterway plus the satellite, cut the bed 0.3 to 0.6 m below the bank, set the water 4 cm above the bed, and keep it off every course mesh.
- *Photo check (slot):* a photo along the creek. Pass when the width within 0.5 m and the bank material match.
- *Default:* a 1.5 m channel 0.4 m deep with rock banks.

### Registration and offsets (misread 5)

**D13. Are the inputs registered to the LiDAR?**
- *LiDAR says:* the LiDAR is the frame. Everything else must be fitted to it.
- *Tierra case:* OSM outlines 1.5 to 3 m off (18); phone LAS scans needed a 1.5 m horizontal shift and 2 to 4 m vertical (the phone's altitude datum); the 10 tee scan needed +1.5 E, −0.5 N and +2.21 m; photo GPS 3 to 5 m usually, 40 m on 4711 and 80 m on 4630 to 4634; the phone compass 50 to 110° off on cart shots; on Penmar and Harding a copied 3000 m constant stretched the satellite 1.5x.
- *Check now:* before any build, fit each input to the LiDAR on something unambiguous (a cart path, a building roof, a pond edge) and record the shift. Satellite square size comes from course.json, never a copied constant. Scans get a least-squares fit (shift, vertical, small rotation) and are rejected over 10 cm median residual. Photo poses come from a LiDAR skyline fit plus landmarks, never from the EXIF heading alone.
- *Photo check (slot):* n/a (this is the step that makes the photo check possible).
- *Default:* no input is used until its shift to the LiDAR is recorded.

**D14. Props and buildings: where is it, really?**
- *LiDAR says:* roofs show as isolated flat blocks even under light canopy (the 13 restroom: a 10.9 x 10.1 m block at (1185, 869)). Small props (enclosures, pump stations, signs, bins) do not show.
- *Tierra case:* the pump station was placed by eye 40 m from where IMG_4763's GPS put it; the hole 14 enclosure was 9.8 m SSW of where the satellite and the solved standing spot put it, which then corrupted the photo poses fitted to it; the restroom door direction was guessed and later fixed from a corrected compass heading.
- *Check now:* every prop gets a world position from the satellite, a LiDAR roof block, or a photo with a solved pose. "By eye" is not a source. Buildings hidden under trees still get built from the roof block.
- *Photo check (slot):* a photo with the prop and a landmark. Pass within 2 m.
- *Default:* not built until it has a source.

### Outer ground (misread 6)

**D15. How far does the real ground go, and what is past it?**
- *LiDAR says:* the clip was the course bbox plus 75 m (about 1.65 x 1.33 km) inside a 2000 m square; the square's edges are already interpolated or outside the survey in places. Anything the flyover sees beyond it is not LiDAR.
- *Tierra case:* the "pale field" right of 15 was Highway 23 and the entrance road; the "orchard across the valley" from 12 was old imagery; the first horizon was a flat plane with a cliff at the course edge, then invented hills; BoJa: "never invent ground, everything visible must come from the real satellite and LiDAR", and "don't clean real features out of the satellite".
- *Check now:* fetch LiDAR (or a coarser DEM) out to the farthest ridge a flyover sees, at least 1 to 2 km. Pull roads, freeways, parking, buildings and the entrance from OSM/Overture and build them wherever a tee, green or flyover frame sees them. Mark the backdrop landmarks (Reagan Library, the far peaks) from bearings off known tees.
- *Photo check (slot):* a photo from each tee toward the horizon. Pass when the skyline fit RMS is under 10 px.
- *Default:* real DEM to the horizon, roads from OSM, no invented hills.

### Layering and mesh order (misread 8)

**D16. Does any lower surface cover a higher one?**
- *LiDAR says:* nothing. This is a build-order problem, but it reads like a shape error on the gate sheet.
- *Tierra case:* Spline_rough_11front was drawn over the collar ring and the green edge on the bunker side, so the putting surface looked 4 m from the lip behind a dark saw-tooth band; the 12 collar skirt showed through a 1 to 2 cm gap under the collar edge; fairway patches cut from rough triangles came out saw-toothed on 16 and 2.
- *Check now:* after any mesh work, test the clip order water > bunker > green > collar > tee > fairway > rough and list every triangle of a lower layer inside a higher layer's outline. Zero is the pass mark. Run the vertex-paint sweep (meshes without paint draw as bare dirt) and the z-fight scan.
- *Photo check (slot):* the low-angle photo of the green edge. Pass when the green edge sits where the photo has it (hole 11: 2 to 2.5 m from the lip).
- *Default:* the clip order above.

### Tees (misread 9)

**D17. Which flat is the tee, and how many decks?**
- *LiDAR says:* a tee box is a levelled flat (slope under 2 %) with a short bank, and it shows well, including boxes OSM never mapped (17 front) and old unused pads (4). Deck heights show (18: 63.3, 64.0 and 65.7 m). The 10 tee scan matched the LiDAR deck to 7.7 cm RMS.
- *Tierra case:* OSM mapped the 5 Black box as a small green; the 4 "crater" was an unused pad we sank into the hill; the 18 tiers were flattened; the 13 Green tee was 12 m in the air; the 10 Black marker was placed at the back of a 17 m deck while the day's markers were 8 m forward; tees sat on paths and on fairway edges on other courses.
- *Check now:* list every LiDAR flat of 40 m² or more within 60 m of an OSM tee or along the hole line; match them to the card's tee colours by yardage; keep decks at their LiDAR heights; a pad never touches a cart path (2 m) or a fairway edge. Keep true distances from the back of the box. Unused pads become rough mounds, never holes.
- *Photo check (slot):* a photo from the box. Pass when the markers sit on the deck the photo shows and the bank height matches.
- *Default:* the LiDAR flat nearest the card yardage.

### Trees and shrubs (misread 10)

**D18. Is a vegetation return a tree, a shrub, a hedge, or a building?**
- *LiDAR says:* height and crown radius per cluster. Not species, not whether it is alive, not whether a roof is under it.
- *Tierra case:* 2,714 shrubs of 3 m or more were planted on tree cards and read as pale blossom trees; a "LiDAR bush" sat under the gazebo roof; species were assigned by rule (over 16 m = eucalyptus, 20 % pepper), then 183 oaks near tees became peppers by another rule, then every pepper was rebuilt from photos; crowns render 1.3x the LiDAR radius, so trees had to stay 1.3x crown + 0.5 m off sand; a tree on 9/1 was the wrong species after the push.
- *Check now:* under 3 m and wider than tall: shrub. Over 3 m: tree, with the species left blank. A cluster over a satellite roof or a known building: not vegetation. Keep every LiDAR tree in play, including dead-looking ones. Clear tee corridors by nudging, not deleting. The orchard, the young palms and anything planted after 2018 come from the satellite date and photos only.
- *Photo check (slot):* a photo of each tree within 35 m of a tee or green. Pass when the species, crown width and foliage colour match. Background trees stay low detail.
- *Default:* coast live oak under 16 m, eucalyptus over, species blank until a photo names it.

### What the LiDAR got right at Tierra (keep these as checks, not decisions)

- Fairway relief: the course meshes keep 95 to 100 % of the LiDAR undulation, RMS 1.3 to 3.1 cm in every landing zone. Don't smooth fairways for looks. Check a cross-section before review.
- Slopes on 16, 10 and 9 matched the survey within centimetres; the 16 false front is 20 to 24 %.
- Tee pads and model tees matched the phone scans within 3 to 14 cm (11 tees).
- Pond and water levels (D11) and roof blocks (D14) are reliable.
- Rangefinder readings on 10, 16, 17 and 18 confirmed the model geometry; it is the scorecard that runs long.

## 4. Pairing this list with photos

When the photo pass is added, each decision point gets its photo slot filled with three things: which photo (by IMG number or a standing spot and bearing), the pass rule already written in the slot, and the verdict. The protocol that worked at Tierra:

1. Pose every photo against the LiDAR first (skyline fit, landmarks, sun azimuth), never from the EXIF heading alone (D13).
2. Render photo | before | after from that pose, one edit at a time.
3. Judge by measured cover, colour ratios and positions, not by impression.
4. In-play turf (tees, fairways, greens, collars) is always built clean even when the photo shows wear. Bare soil only on out-of-play banks.
5. Photos win over the LiDAR on bunker faces, banks, lips and anything the 1 m grid can't hold. The LiDAR wins on green surfaces, water levels and fairway relief. A clean scan wins over both.

## 5. Next inputs that would remove the guessing

- A 2023 or later QL1 survey if one ever reaches the course (CA_LosAngeles_1_B23 stops 12.5 km short).
- A 3D Scanner LAS per green and per greenside bunker complex, before any green is built.
- A GPS-tagged photo from each tee box and each green, plus one along each pond bank and each hillside toe.
- A DEM out to the horizon and the OSM road and building layers, fetched with the LiDAR on day one.

## 6. Applying this to the pipeline courses (BoJa, 10/31)

Once Tierra is done, every course in the pipeline (Penmar, Harding, Wilson, Hansen Dam, Angeles National, Moorpark, Los Robles and later ones) runs D1 to D18 before its first gate. Record the answers in that course's `decisions.csv`. Courses with denser or newer LiDAR settle more points from data, but some points are never in the data, however good the scan. Those are flagged **in-person review needed**, and a hole is not built to final quality until someone has stood there.

Each course README states its survey. Tierra, Hansen Dam and Moorpark are on the 2018 QL2 survey (about 4 pts/m²). Angeles National is on CA_LosAngeles_1_B23 (QL1, 2023 to 2024, 38 to 45 pts/m², 11 to 25 ground). Check the line in each README rather than assuming.

| Point | 2018 QL2 (Tierra, Hansen, Moorpark) | 2023 QL1 or a phone scan | In-person review needed? |
|---|---|---|---|
| D1 cover class | Brush mask only; grass vs turf unresolved | Grass height 0.2 m up resolves, so dry grass vs mown shows; species and colour still don't | **Yes**: colour, species, soil share |
| D2 mow line | Satellite only | Satellite only | Yes where the satellite is old or dry |
| D3 rock vs scrub | Slope only | Face shape resolves at 0.25 m; bare rock vs scrub still from imagery | **Yes**: rock extent and colour |
| D4 sand edge | Satellite wins | The depression edge resolves to about 0.3 m | No, if the satellite is recent |
| D5 bunker depth and face | Under-read; use rule depths | Depth and face read within about 10 cm | Yes on QL2; photo check on QL1 |
| D6 island or nose | Not in data | Not in data | **Yes** |
| D7 green surface | Needs a scan | QL1 gives tiers and 10 cm breaks; a phone LAS still wins on the surface | Scan visit per green |
| D8 green outline vs pad | Fit plus satellite | Pad edge resolves to about 0.3 m | No |
| D9 collar and cut step | Not in data | Not in data | **Yes**: width, step, surround mow |
| D10 beyond-LiDAR banks | Photos needed | Banks resolve; rarely needed | Photo check only |
| D11 water level | Reliable | Reliable | No |
| D12 creek channel | Mostly not in data | Channel resolves if over 0.5 m wide | Yes on QL2 |
| D13 registration | Always | Always | No |
| D14 props and buildings | Roofs only | Roofs, enclosures over 1 m, poles | **Yes** for anything smaller than 1 m and every door, sign and bin |
| D15 outer ground | Fetch wider | Fetch wider | No; Street View fills roads and entrances |
| D16 mesh order | Build check | Build check | No |
| D17 tee decks | Flats resolve | Flats and steps resolve | Yes for which colour is on which deck |
| D18 tree vs shrub, species | Height classes only | Crown shape resolves; species still doesn't | **Yes** for every tree within 35 m of a tee or green |

Rule for the pipeline: a course on QL1 data skips the data-side guesses in D4, D5, D8, D12 and D17, and still gets the in-person list (D1, D3, D6, D9, D14, D18, plus a scan per green for D7). The in-person list for a course is written out per hole before the visit, with the photo or scan each point needs, so one visit covers it.
