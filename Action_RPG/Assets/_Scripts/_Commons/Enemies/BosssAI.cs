/*
// Trong BossAI.cs (Ví dụ tương lai)

public class BossAI : EnemyAI 
{
    private BossCombat bossCombat;

    void Start() {
        // ... (Setup cũ)
        bossCombat = GetComponent<BossCombat>(); // Lấy component con
    }

    void Update() {
        // ... (Logic AI bình thường) ...

        // Ví dụ: Logic né đòn thông minh
        // Nếu Player đang đánh (kiểm tra Player.isAttacking) và Boss đang ở gần
        // -> Boss hủy đòn đánh hiện tại để lướt ra sau lưng Player
        
        if (PlayerIsAttacking() && IsInDanger()) 
        {
            Vector3 dodgeDir = -transform.forward; // Lướt lùi
            bossCombat.PerformBossDash(dodgeDir);
        }
    }
}
*/