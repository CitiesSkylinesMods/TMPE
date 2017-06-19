# TM:PE -- /Custom/Geometry
Classes for pre-calculated properties of segments, segment ends and nodes.
## Terminology
Although the term *geometry* is being used, we are not storing angles and lengths of segments. We are instead storing relational information of two or more segments that are connected at a node (e.g. segment X is left of segment Y).

The term *segment end* represents the directional component of traffic at one segment. For example, a segment end at segment X and node Y represent the set of all lanes that allow traffic to flow from X to Y. Thus, for each segment that is not a one-way street and is connected to two nodes, two separate segment ends exist: One describing the segment's part connected to its start node and the other segment end describes the situation at the segment's end node.

*Incoming* traffic at segment ends always flows to the node where *outgoing* traffic flows away from the node.
## Classes
- **GeometryCalculationMode**: Controls propagation when performing geometry calculations.
- **NodeGeometry**: Holds all connected segment end geometries.   
- **SegmentEndGeometry**: Stores information about a segment at one connected node.
- **SegmentEndId**: Abstract class to represent segment ends
- **SegmentGeometry**: Stores general information about a segment and holds references to both segment ends.