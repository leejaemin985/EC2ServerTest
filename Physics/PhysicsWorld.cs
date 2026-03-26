using LJMCollision;

/// <summary>
/// 물리 월드. PhysicsBody들을 관리하고, 매 틱 물리 시뮬레이션을 수행한다.
/// 쿼리 API는 PhysicsWorld.Query.cs 참조
/// </summary>
public partial class PhysicsWorld
{
    public const float DefaultGravity = -30f;

    readonly List<PhysicsBody> _bodies = new();
    readonly List<PhysicsBody> _pendingAdd = new();
    readonly List<PhysicsBody> _pendingRemove = new();

    /// <summary>정적 맵 충돌 (LJMCollision)</summary>
    public CollisionWorld StaticWorld { get; set; }

    /// <summary>레이어 간 충돌 매트릭스. 기본값: 모든 레이어 충돌 가능.</summary>
    public LayerMatrix LayerMatrix { get; } = new();

    /// <summary>등록된 바디 목록 (읽기 전용)</summary>
    public IReadOnlyList<PhysicsBody> Bodies => _bodies;

    // ── 등록 / 제거 ──

    public void Add(PhysicsBody body) => _pendingAdd.Add(body);
    public void Remove(PhysicsBody body) => _pendingRemove.Add(body);

    // ── 매 틱 처리 ──

    /// <summary>물리 틱. GameLoop의 PostTick 또는 별도 단계에서 호출.</summary>
    public void Step(float deltaTime)
    {
        FlushPending();
        ApplyGravityAndDrag(deltaTime);
        MoveAndCollide(deltaTime);
        ResolveDynamicCollisions();
    }

    void ApplyGravityAndDrag(float deltaTime)
    {
        foreach (var body in _bodies)
        {
            if (body.BodyType != BodyType.Dynamic) continue;
            if (!body.Active) continue;

            if (body.UseGravity)
            {
                body.Velocity = new Vec3(
                    body.Velocity.X,
                    body.Velocity.Y + DefaultGravity * deltaTime,
                    body.Velocity.Z);
            }

            if (body.Drag < 1f)
            {
                float drag = MathF.Pow(body.Drag, deltaTime * 10f);
                body.Velocity = new Vec3(
                    body.Velocity.X * drag,
                    body.Velocity.Y,
                    body.Velocity.Z * drag);
            }
        }
    }

    void MoveAndCollide(float deltaTime)
    {
        foreach (var body in _bodies)
        {
            if (body.BodyType != BodyType.Dynamic) continue;
            if (!body.Active) continue;

            Vec3 displacement = body.Velocity * deltaTime;

            if (StaticWorld != null)
            {
                Vec3 newCenter = body.Shape.MoveAndSlide(StaticWorld, body.Position, body.Rotation, displacement);

                if (body.UseGravity)
                {
                    float bottomOffset = body.Shape.BottomOffset;
                    float checkRadius = body.Shape.HorizontalRadius;
                    float feetY = newCenter.Y - bottomOffset;

                    if (StaticWorld.GroundCheck(new Vec3(newCenter.X, feetY + 0.1f, newCenter.Z), checkRadius, out float groundY, out var groundNormal))
                    {
                        // 경사 각도 계산 (Normal과 Up의 각도)
                        float slopeAngle = MathF.Acos(MathF.Min(groundNormal.Y, 1f)) * (180f / MathF.PI);
                        bool walkable = slopeAngle <= body.MaxSlopeAngle;

                        if (feetY <= groundY + 0.05f)
                        {
                            if (walkable)
                            {
                                newCenter = new Vec3(newCenter.X, groundY + bottomOffset, newCenter.Z);
                                body.Grounded = true;

                                if (body.Velocity.Y < 0f)
                                    body.Velocity = new Vec3(body.Velocity.X, 0f, body.Velocity.Z);
                            }
                            else
                            {
                                // 경사가 너무 가파름 — 미끄러짐
                                body.Grounded = false;
                            }
                        }
                        else
                        {
                            body.Grounded = false;
                        }
                    }
                    else
                    {
                        body.Grounded = false;
                    }
                }

                body.Position = new Vec3(newCenter.X, newCenter.Y - body.Shape.BottomOffset, newCenter.Z);
            }
            else
            {
                body.Position += displacement;
            }
        }
    }

    void ResolveDynamicCollisions()
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            var a = _bodies[i];
            if (a.BodyType == BodyType.Trigger) continue;
            if (!a.Active) continue;

            for (int j = i + 1; j < _bodies.Count; j++)
            {
                var b = _bodies[j];
                if (b.BodyType == BodyType.Trigger) continue;
                if (!b.Active) continue;
                if (!LayerMatrix.CanCollide(a.Layer, b.Layer)) continue;

                var result = TestShapePair(a, b);
                if (!result.Hit) continue;

                // TODO: 수평 방향으로만 밀어냄 — 플레이어끼리는 맞지만, 수직 충돌이 필요한 조합에선 잘못된 결과. 플래그로 분리 필요.
                Vec3 normal = new Vec3(result.Normal.X, 0f, result.Normal.Z);
                float len = normal.Magnitude;
                if (len < MathUtils.Epsilon) normal = Vec3.Right;
                else normal = normal * (1f / len);

                // 질량 비율로 분배 (Mass 0 = 무한질량 = 안 밀림)
                float massA = a.BodyType == BodyType.Static ? 0f : a.Mass;
                float massB = b.BodyType == BodyType.Static ? 0f : b.Mass;
                float totalInvMass = (massA > 0f ? 1f / massA : 0f) + (massB > 0f ? 1f / massB : 0f);

                if (totalInvMass <= 0f) continue; // 둘 다 무한질량이면 스킵

                float ratioA = massA > 0f ? (1f / massA) / totalInvMass : 0f;
                float ratioB = massB > 0f ? (1f / massB) / totalInvMass : 0f;

                a.Position += normal * (result.Depth * ratioA);
                b.Position -= normal * (result.Depth * ratioB);
            }
        }
    }

    /// <summary>두 바디의 Shape 조합에 따라 충돌 검출</summary>
    static CollisionResult TestShapePair(PhysicsBody a, PhysicsBody b)
    {
        Vec3 posA = a.Position;
        Vec3 posB = b.Position;

        switch (a.Shape, b.Shape)
        {
            case (CapsuleShape capA, CapsuleShape capB):
                return CollisionDetection.CapsuleVsCapsule(capA.ToCapsule(posA), capB.ToCapsule(posB));

            case (CapsuleShape cap, SphereShape sph):
                var r1 = CollisionDetection.SphereVsCapsule(sph.ToSphere(posB), cap.ToCapsule(posA));
                return new CollisionResult { Hit = r1.Hit, Normal = -r1.Normal, Depth = r1.Depth }; // normal 반전 (A 기준)

            case (SphereShape sph, CapsuleShape cap):
                return CollisionDetection.SphereVsCapsule(sph.ToSphere(posA), cap.ToCapsule(posB));

            case (CapsuleShape cap, BoxShape box):
                return CollisionDetection.CapsuleVsOBB(cap.ToCapsule(posA), box.ToOBB(posB, b.Rotation));

            case (BoxShape box, CapsuleShape cap):
                var r2 = CollisionDetection.CapsuleVsOBB(cap.ToCapsule(posB), box.ToOBB(posA, a.Rotation));
                return new CollisionResult { Hit = r2.Hit, Normal = -r2.Normal, Depth = r2.Depth };

            case (SphereShape sphA, SphereShape sphB):
                return CollisionDetection.SphereVsSphere(sphA.ToSphere(posA), sphB.ToSphere(posB));

            case (SphereShape sph, BoxShape box):
                return CollisionDetection.SphereVsOBB(sph.ToSphere(posA), box.ToOBB(posB, b.Rotation));

            case (BoxShape box, SphereShape sph):
                var r3 = CollisionDetection.SphereVsOBB(sph.ToSphere(posB), box.ToOBB(posA, a.Rotation));
                return new CollisionResult { Hit = r3.Hit, Normal = -r3.Normal, Depth = r3.Depth };

            case (BoxShape boxA, BoxShape boxB):
                return CollisionDetection.OBBVsOBB(boxA.ToOBB(posA, a.Rotation), boxB.ToOBB(posB, b.Rotation));

            default:
                return CollisionResult.None;
        }
    }


    void FlushPending()
    {
        if (_pendingAdd.Count > 0)
        {
            _bodies.AddRange(_pendingAdd);
            _pendingAdd.Clear();
        }
        if (_pendingRemove.Count > 0)
        {
            foreach (var body in _pendingRemove)
                _bodies.Remove(body);
            _pendingRemove.Clear();
        }
    }

}
