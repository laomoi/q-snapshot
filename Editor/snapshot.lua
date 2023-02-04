local qsnapshot = {}

rawset(_G, 'qsnapshot', qsnapshot)

local weak_meta_table = {__mode = 'k', __ignore_qsnapshot = true}
setmetatable(qsnapshot, weak_meta_table)


qsnapshot.get_metatable = function(t)
    return getmetatable(t)
end

qsnapshot.get_registry = function()
    return debug.getregistry()
end

qsnapshot.get_package_loaded = function()
    if package ~= nil and package.loaded ~= nil then
        return package.loaded
    end
    return nil
end


qsnapshot.get_func_source = function(func)
    local info = debug.getinfo(func, 'Sl')
    local result = string.format('%s&line:%d', info.source, info.linedefined)
    return result
end

qsnapshot.get_func_source_and_tag = function(func)
    local info = debug.getinfo(func, 'Sl')
    local result = string.format('%s&line:%d', info.source, info.linedefined)

    local tag = qsnapshot.get_function_tag(func, info)
    return result,tag
end


   
qsnapshot.get_memory_usage = function()
    return math.floor(collectgarbage("count"))
end

-------------------------------------------------------------------------
------------below functions can be modified for tagging objects you need

qsnapshot.get_function_tag = function(func, info)
    -- if info.source == '@base/utility/class.lua' and info.linedefined == 241 then
    --     local i = 1
    --     while true do
    --         local k, v = debug.getupvalue(func, i)
    --         if  k == nil then
    --             break
    --         end
    --         if k == "trackback2" then
    --             return v
    --         end
    --         i = i + 1
    --     end
    -- end
    return nil
end

qsnapshot.get_userdata_tag = function(u)
    --add tag to userdata
    return nil
end

qsnapshot.get_table_tag = function(t)
    --add tag to table 
    if t ~= nil then
        local cls = rawget(t, "__class_name")
        local id = rawget(t, "id")
        local tag = nil
        if cls ~= nil then
            tag = "cls:" .. cls
        end
        return tag
    end

    return nil
end


return qsnapshot
