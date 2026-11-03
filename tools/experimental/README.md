# experimental

Scripts that did a job once and are candidates for real tools. Not on the README table, not tested, paths may be course-shaped.

- `lidar_review.py` + `write_outputs.py`: produced the Hansen Dam D1-D18 draft (course_photos/decisions.csv, visit_list.md,
  progress/lidar_review_1103.md) on 2026-11-03 from the heightmap, SVG, GKD, satellite and trees. Candidate for `tools/lidar_review.py`
  (the decisions.csv writer the lidar-review skill lists as missing). Generalise: paths from course.json, thresholds from
  rules/lidar_review.json, QL from course.json lidar.quality_level.
