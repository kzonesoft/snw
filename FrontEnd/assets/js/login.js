function checkTokenAndRedirect() {
    const token = sessionStorage.getItem('token');
    if (token) {
        // Nếu token tồn tại, chuyển hướng đến trang /index
        window.location.href = '/index';
    }
}

function handleFormSubmit(event) {
    event.preventDefault(); // Ngăn form gửi dữ liệu mặc định
    login(); // Gọi hàm login
}

async function login() {
    try {
        const password = document.getElementById('password').value;

        const response = await fetch('/api/login', {
            method: 'POST',
            headers: { 'Content-Type': 'text/plain' },
            body: password
        });

        const data = await response.json();
        const message = document.getElementById('message');

        if (data.success) {
            console.log("send api login ok ");
            // Lưu token vào localStorage
            sessionStorage.setItem('token', data.token);

            // Hiển thị thông báo thành công
            message.style.color = 'green';
            message.textContent = data.message;

            window.location.href = '/index';

        } else {
            // Nếu thất bại, xóa token khỏi localStorage nếu tồn tại
            if (sessionStorage.getItem('token')) {
                sessionStorage.removeItem('token');
            }

            // Hiển thị thông báo lỗi
            message.style.color = 'red';
            message.textContent = data.message;
        }
    } catch (err) {
        console.error(err);
        alert('Có lỗi xảy ra!');
    }
}