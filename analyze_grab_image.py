from PIL import Image
import numpy as np

image_path = r"FluentClip帧动画素材\Grab\Grab.png"

img = Image.open(image_path)
img_array = np.array(img)

print(f"图片尺寸: {img.size}")
print(f"图片模式: {img.mode}")

if len(img_array.shape) == 3 and img_array.shape[2] == 4:
    r, g, b, a = img_array[:,:,0], img_array[:,:,1], img_array[:,:,2], img_array[:,:,3]
    print(f"Alpha 通道范围: {a.min()} - {a.max()}")
    print(f"完全透明像素: {np.sum(a == 0)}")
    print(f"完全不透明像素: {np.sum(a == 255)}")
    print(f"半透明像素: {np.sum((a > 0) & (a < 255))}")
    
    content_mask = a > 10
else:
    print("无Alpha通道")
    content_mask = None

if content_mask is not None:
    rows_with_content = np.where(np.any(content_mask, axis=1))[0]
    cols_with_content = np.where(np.any(content_mask, axis=0))[0]
    
    if len(rows_with_content) > 0 and len(cols_with_content) > 0:
        min_y, max_y = rows_with_content[0], rows_with_content[-1]
        min_x, max_x = cols_with_content[0], cols_with_content[-1]
        
        width = max_x - min_x + 1
        height = max_y - min_y + 1
        
        print(f"\n{'='*50}")
        print(f"🐱 猫叼鱼图片边界检测结果")
        print(f"{'='*50}")
        print(f"原始图片尺寸: {img.width} x {img.height}")
        print(f"非透明区域边界:")
        print(f"  左上角: ({min_x}, {min_y})")
        print(f"  右下角: ({max_x}, {max_y})")
        print(f"  宽度: {width} 像素")
        print(f"  高度: {height} 像素")
        
        scale = 0.25
        scaled_width = int(width * scale)
        scaled_height = int(height * scale)
        
        print(f"\n{'='*50}")
        print(f"📐 悬浮窗配置 (原始尺寸)")
        print(f"{'='*50}")
        print(f"窗口尺寸: Width=\"{width}\" Height=\"{height}\"")
        print(f"Margin: \"{min_x},{min_y},0,0\"")
        
        print(f"\n{'='*50}")
        print(f"📐 悬浮窗配置 (缩放 {scale*100:.0f}%)")
        print(f"{'='*50}")
        print(f"窗口尺寸: Width=\"{scaled_width}\" Height=\"{scaled_height}\"")
        print(f"Margin: \"{int(min_x * scale)},{int(min_y * scale)},0,0\"")
        
        print(f"\n{'='*50}")
        print(f"💻 WPF XAML 代码")
        print(f"{'='*50}")
        print(f'''<Window x:Class="FluentClip.TrayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="FluentClip"
        Width="{scaled_width}" Height="{scaled_height}"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        Left="{int(min_x * scale)}" Top="{int(min_y * scale)}"
        MouseLeftButtonDown="Window_MouseLeftButtonDown">
    <Grid>
        <Image Source="/FluentClip帧动画素材/Grab/Grab.png" 
               Stretch="Uniform"
               Width="{scaled_width}" Height="{scaled_height}"/>
    </Grid>
</Window>''')
        
        bbox_image = Image.new('RGBA', (img.width, img.height), (0, 0, 0, 0))
        from PIL import ImageDraw
        draw = ImageDraw.Draw(bbox_image)
        draw.rectangle([min_x, min_y, max_x, max_y], outline=(255, 0, 0, 255), width=3)
        
        for y in range(min_y, max_y + 1):
            for x in range(min_x, max_x + 1):
                if a[y, x] > 0:
                    bbox_image.putpixel((x, y), (r[y, x], g[y, x], b[y, x], 180))
        
        output_path = r"FluentClip帧动画素材\Grab\Grab_result.png"
        bbox_image.save(output_path)
        print(f"\n✅ 结果图已保存到: {output_path}")
else:
    print("未检测到内容区域")
