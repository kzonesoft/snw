// Sử dụng IIFE (Immediately Invoked Function Expression) để tránh xung đột biến toàn cục
(function () {
    let updateInterval = null;
    let isUpdating = false;

    const downloadedModule = {
        updateData: async function () {
            if (!isUpdating) return;

            try {
                const token = sessionStorage.getItem('token'); // Lấy token từ sessionStorage
                if (!token) {
                    window.location.href = '/login'; // Điều hướng đến trang login
                    this.stopAutoUpdate();
                    return;
                }

                document.getElementById('total-games').textContent = '...'; // Hiển thị đang tải

                const response = await fetch('/api/games/downloaded', {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`,
                    },
                });

                if (response.status === 401) {
                    // Nếu response là 401, xóa token và làm mới trang
                    sessionStorage.removeItem('token');
                    window.location.href = '/login';
                    this.stopAutoUpdate();
                    return;
                }

                const data = await response.json();
                const totalGames = data.length;
                document.getElementById('total-games').textContent = totalGames;

                const tbody = document.getElementById('data-body');
                tbody.innerHTML = '';

                // Hàm định dạng ngày tháng
                const formatDate = (dateStr) => {
                    if (!dateStr) return 'N/A';

                    try {
                        const date = new Date(dateStr);
                        return date.toLocaleDateString('vi-VN', {
                            year: 'numeric',
                            month: '2-digit',
                            day: '2-digit',
                            hour: '2-digit',
                            minute: '2-digit'
                        });
                    } catch (error) {
                        console.error('Error formatting date:', error);
                        return 'N/A';
                    }
                };

                data.forEach(item => {
                    // Hiển thị tên (dùng displayName nếu có, nếu không thì dùng name)
                    const gameName = item.displayName || item.name || 'N/A';

                    // Tạo row cho từng game
                    const row = `
                        <tr>
                            <td class="status-blue">${gameName}</td>
                            <td>${item.version || 'N/A'}</td>
                            <td>${item.size || 0}</td>
                            <td>${item.clientDisk ? item.clientDisk : 'N/A'}</td>
                            <td>${item.category || 'N/A'}</td>
                            <td>${item.launcher || 'N/A'}</td>
                            <td>${formatDate(item.downloadFinish)}</td>
                        </tr>
                    `;
                    tbody.innerHTML += row;
                });
            } catch (error) {
                console.error('Error fetching downloaded games info:', error);
            }
        },

        startAutoUpdate: function () {
            console.log('Downloaded Games: Starting auto update');
            if (isUpdating) {
                console.log('Downloaded Games: Already updating, no action needed');
                return;
            }

            // Đảm bảo dừng cập nhật cũ nếu có
            this.stopAutoUpdate();

            // Đánh dấu trạng thái đang cập nhật
            isUpdating = true;

            // Cập nhật ngay lập tức
            this.updateData();

            // Thiết lập cập nhật định kỳ
            updateInterval = setInterval(() => this.updateData(), 30000); // Cập nhật mỗi 30 giây
        },

        stopAutoUpdate: function () {
            if (!isUpdating && !updateInterval) {
                console.log('Downloaded Games: Not updating, no action needed');
                return;
            }

            console.log('Downloaded Games: Stopping auto update');

            // Dừng đồng hồ cập nhật
            if (updateInterval) {
                clearInterval(updateInterval);
                updateInterval = null;
            }

            // Cập nhật trạng thái
            isUpdating = false;
        }
    };

    // Đảm bảo dừng cập nhật nếu trang bị tắt hoặc thay đổi
    window.addEventListener('beforeunload', () => {
        downloadedModule.stopAutoUpdate();
    });

    // Gán các phương thức vào window để có thể gọi từ index.js
    window.startAutoUpdate = function () {
        downloadedModule.startAutoUpdate();
    };

    window.stopAutoUpdate = function () {
        downloadedModule.stopAutoUpdate();
    };

    // Không tự động khởi động khi script được load
    // Sẽ do index.js gọi window.startAutoUpdate() sau khi script được load hoàn tất
})();