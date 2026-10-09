# STEPS:

1. Requirements

2. Entities

3. Class Design

4. Implementation

5. Extensibility


# TIPS:

Requirements:

1. To convert vague problem statement into concrete requirements, write it in below format:
   - Primary Capabilities
   - Error/Edge Case Handling
   - Out of Scope


Entities:

1. Identify Entities from Concrete Requirements
   - Typically Nouns

2. Feel free to add supporting Entities/Enums/Record/Struct as you design/implement

3. When transitioning from single state to multi-dimensional state, introduce enum


Class Design:

1. Start Class Design from Higher Component of System to Lower Component of System

2. Use Visibility Markers for Properties and Behavior of Classes
   - Public [+]
   - Private [-]
   - Protected [#]

3. Use consistent naming conventions for Properties/Behavior
   - Properties
     - Private: camalCase
     - Public/Protected: PascalCase

   - Behavior/Methods
     - Public/Private/Protected: PascalCase

4. While writing Properties or Behavior, look at each Concrete Requirements


Implementation:

1. For each Behavior/Method, follow this pattern:
   - Define Core Logic: Happy path that fulfills the requirement 
   - Handle Error/Edge Cases: Invalid inputs, boundary conditions, unexpected states
   - Implement Happy Path then Error/Edge Cases

2. Interviewer usually focus on the most interesting methods/hero use cases, implement them before

3. Prefer adding the helper behavior/methods as you go, decide with interviewer if those needs an implementation
   - Helpers mostly have Private Visibility


# NOTE:

You can always go back and modify Entities/Class Design/Implementation upon discussion with interviewer.