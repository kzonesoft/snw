// Sử dụng IIFE (Immediately Invoked Function Expression) để tránh xung đột biến toàn cục
(function () {
    let updateInterval = null;
    let isUpdating = false;

    const hwInfoModule = {
        updateData: async function () {
            if (!isUpdating) return;

            try {
                const token = sessionStorage.getItem('token'); // Lấy token từ sessionStorage
                if (!token) {
                    window.location.href = '/login'; // Điều hướng đến trang login
                    this.stopAutoUpdate();
                    return;
                }

                document.getElementById('total-clients').textContent = '...'; // Hiển thị đang tải

                const response = await fetch('/api/wks/hwinfo', {
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
                const totalClients = data.length;
                document.getElementById('total-clients').textContent = totalClients;

                const tbody = document.getElementById('data-body');
                tbody.innerHTML = '';

                data.forEach(item => {
                    // Chuyển tên thuộc tính về dạng đúng (từ response của server)
                    // Backend trả về dạng WksName, CpuName... nên cần đảm bảo truy cập đúng
                    const wksName = item.WksName || item.wksName || 'N/A';
                    const cpuName = item.CpuName || item.cpuName || 'N/A';
                    const gpuName = item.GpuName || item.gpuName || 'N/A';
                    const mainboardName = item.MainboardName || item.mainboardName || 'N/A';
                    const lanName = item.LanName || item.lanName || 'N/A';
                    const ramTotal = item.RamTotal || item.ramTotal || '0';
                    const virtualization = item.Virtualization || item.virtualization || false;
                    const mac = item.Mac || item.mac || 'N/A';

                    const row = `
                        <tr>
                            <td class="status-blue">${wksName}</td>
                            <td>${cpuName}</td>
                            <td>${gpuName}</td>
                            <td>${mainboardName}</td>
                            <td>${lanName}</td>
                            <td>${ramTotal} GB</td>
                            <td>${virtualization ? 'Bật' : 'Tắt'}</td>
                            <td>${mac}</td>
                        </tr>
                    `;
                    tbody.innerHTML += row;
                });
            } catch (error) {
                console.error('Error fetching hardware info:', error);
            }
        },

        startAutoUpdate: function () {
            console.log('HW Info: Starting auto update');
            if (isUpdating) {
                console.log('HW Info: Already updating, no action needed');
                return;
            }

            // Đảm bảo dừng cập nhật cũ nếu có
            this.stopAutoUpdate();

            // Đánh dấu trạng thái đang cập nhật
            isUpdating = true;

            // Cập nhật ngay lập tức
            this.updateData();

            // Thiết lập cập nhật định kỳ
            updateInterval = setInterval(() => this.updateData(), 30000);
        },

        stopAutoUpdate: function () {
            if (!isUpdating && !updateInterval) {
                console.log('HW Info: Not updating, no action needed');
                return;
            }

            console.log('HW Info: Stopping auto update');

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
        hwInfoModule.stopAutoUpdate();
    });

    // Gán các phương thức vào window để có thể gọi từ index.js
    window.startAutoUpdate = function () {
        hwInfoModule.startAutoUpdate();
    };

    window.stopAutoUpdate = function () {
        hwInfoModule.stopAutoUpdate();
    };

    // Không tự động khởi động khi script được load
    // Sẽ do index.js gọi window.startAutoUpdate() sau khi script được load hoàn tất
})();