# Action RPG

## Tổng quan
Đây là một dự án **Action RPG** tập trung vào **lối chơi chiến đấu dựa trên không gian (Spatial Combat)**.

Ý tưởng cốt lõi của game là:
> **Sức mạnh của người chơi đến từ khả năng kiểm soát vị trí và hướng đánh**,  
> không chỉ từ chỉ số nhân vật hay spam kỹ năng.

Hiệu quả chiến đấu phụ thuộc vào:
- Vị trí tương đối giữa người chơi và kẻ địch
- Hướng tấn công (trước / bên hông / sau lưng)
- Hướng nhìn và quán tính xoay của kẻ địch
- Địa hình và vật cản trong bản đồ

---

## Tầm nhìn (Vision)
- Xây dựng một Action RPG có **skill ceiling cao**
- Không sử dụng cơ chế canh nhịp, QTE hay rhythm-based
- Thưởng cho người chơi **đánh thông minh**, không phải bấm nhanh
- Người chơi giỏi luôn chơi hiệu quả hơn người chơi thường, ngay cả khi chỉ số tương đương

---

## Trụ cột Gameplay

### 1. Spatial Combat
Chiến đấu được quyết định bởi:
- Hướng đánh
- Vị trí đứng
- Hướng nhìn của kẻ địch

Không gian trong combat là một **yếu tố chiến thuật**, không phải cảnh nền.

---

### 2. Sát thương theo hướng (Directional Damage)
Tất cả đòn đánh và kỹ năng đều chịu ảnh hưởng bởi hướng tấn công:

- **Đánh chính diện**: ~1.0× sát thương  
- **Đánh bên hông**: ~1.25 - 1.75× sát thương  
- **Đánh sau lưng**: ~2.0× sát thương  

Áp dụng cho:
- Đòn đánh thường
- Kỹ năng chủ động
- Signature Skill

Cùng một kỹ năng, nhưng người chơi khác nhau sẽ tạo ra hiệu quả khác nhau.

---

### 3. Hệ thống hướng của kẻ địch
Kẻ địch không phải mục tiêu đứng yên.

Mỗi enemy có:
- Hướng nhìn (Facing Direction)
- Điểm yếu theo hướng (Weak Side / Weak Back)
- Tốc độ và quán tính xoay người (không thể xoay tức thì)

Đánh sai vị trí có thể:
- Giảm sát thương
- Giảm hiệu quả khống chế
- Khiến kẻ địch trở nên nguy hiểm hơn

---

### 4. Triết lý thiết kế kỹ năng
Kỹ năng **không bị giới hạn vai trò**.

Một kỹ năng có thể:
- Gây sát thương
- Buff hoặc debuff
- Khống chế (CC)
- Bao gồm di chuyển (dash, nhảy, reposition)

**Tuy nhiên:**
- Hiệu quả kỹ năng luôn tuân theo luật không gian
- Không có kỹ năng nào bỏ qua hoàn toàn hướng đánh

---

### 5.Phối hợp đội hình
- Người chơi điều khiển 1 nhân vật tại một thời điểm
- AI sẽ điều khiển 1 người bạn đồng hành đi cùng nhân vật
- Synergy đội hình dựa trên **kiểm soát vị trí**, không phải xoay vòng kỹ năng

---

## Luồng chiến đấu (Combat Flow)
1. Quan sát hướng và trạng thái của kẻ địch
2. Di chuyển hoặc dùng kỹ năng để tạo vị trí có lợi
3. Tấn công từ góc mạnh (hông hoặc sau lưng)
4. Khai thác điểm yếu để gây sát thương lớn

---

## Kỹ năng người chơi vs Sức mạnh nhân vật
- Kỹ năng người chơi quan trọng hơn chỉ số
- Chỉ số giúp ổn định, không thay thế tư duy chiến đấu
- Không có cơ chế auto-hit hoặc auto-crit vô điều kiện

---

## Phạm vi hiện tại
Repository này hiện tập trung vào:
- Ý tưởng gameplay cốt lõi
- Hệ thống combat dựa trên không gian
- Các quy tắc thiết kế (design rules)

Chưa bao gồm:
- Cốt truyện và lore
- Đồ họa và UI
- Monetization

---

## Hướng phát triển tiếp theo
Dự kiến mở rộng:
- Bộ nhân vật khởi đầu với vai trò không gian khác nhau
- Boss có cơ chế phạt người chơi đứng sai vị trí
- Thiết kế bản đồ phục vụ directional combat
- Hệ thống progression không gây power creep

---

## Trạng thái dự án
🚧 Giai đoạn ý tưởng & prototype gameplay  
Dự án đang được phát triển theo hướng thử nghiệm và tinh chỉnh thiết kế.

---

## Giấy phép
Chưa quyết định.
